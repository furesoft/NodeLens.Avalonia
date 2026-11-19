using System.Text;
using Projectional.Core;
using Projectional.Language;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Projectional.Toml;

/// <summary>Turns TOML text into a document of this language, once.</summary>
/// <remarks>
/// <para>
/// <b>The lexer is the package's, the mapping is ours.</b> <c>Tomlyn</c> owns the parts of TOML that are easy to get
/// wrong - the four string kinds, four date and time kinds, underscored and hexadecimal numbers, escapes - and this
/// class owns only the mapping. Nothing here is a hand-written grammar.
/// </para>
/// <para>
/// <b>Every leaf keeps the file's spelling.</b> A string holds the text between its delimiters, an integer and a float
/// hold their literal, a date holds its text; the kind of a string and of a date is a slot beside the text. That is
/// what makes generation byte-exact without an escaping part in the template vocabulary.
/// </para>
/// <para>
/// <b>The order is the structure.</b> A pair belongs to the table whose header was written last, so the importer keeps
/// one open container while it walks the file: the document, or the table the last header opened. Nothing is sorted,
/// grouped or nested otherwise.
/// </para>
/// <para>
/// <b>A duplicate key is refused.</b> This is the one place the four reference languages disagree on purpose: JSON keeps
/// a duplicate member because a consumer sees both, TOML makes it a syntax error, so the document is refused and nothing
/// is imported - a language does not get to be more permissive than the format it mirrors.
/// </para>
/// <para>
/// <b>A comment inside an array moves.</b> The model has no entry list inside an array, so a comment that stood between
/// two of its items becomes an entry of the table the pair belongs to, which is reported as a warning. The text is kept;
/// only its line moves, and a round trip sees that only for a file this language did not write.
/// </para>
/// </remarks>
public sealed class TomlImporter : ITextImporter
{
    /// <summary>The diagnostic for text that is not a TOML document.</summary>
    public const string UnexpectedToken = "toml.unexpected-token";

    /// <summary>The diagnostic for a key that is defined twice in one table.</summary>
    public const string DuplicateKey = "toml.duplicate-key";

    /// <summary>The diagnostic for a comment the model had to move out of an array.</summary>
    public const string CommentMoved = "toml.comment-moved";

    /// <summary>The format name a host's file dialog filters on.</summary>
    public string Format => "toml";

    /// <summary>Whether the text starts like a TOML document.</summary>
    /// <param name="text">The text to look at.</param>
    /// <remarks>
    /// A cheap shape question on the first meaningful line, never a parse: a TOML file starts with a pair
    /// (<c>key = value</c>), a header (<c>[table]</c>) or a comment. A colon instead of an equals sign is what tells it
    /// from YAML, and the two cannot both be right about the same line. The answer that matters is
    /// <see cref="Import"/>'s.
    /// </remarks>
    public bool CanImport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            if (trimmed[0] == '[')
            {
                return true;
            }

            var equals = trimmed.IndexOf('=', StringComparison.Ordinal);
            var colon = trimmed.IndexOf(':', StringComparison.Ordinal);

            return equals > 0 && (colon < 0 || equals < colon);
        }

        return false;
    }

    /// <inheritdoc/>
    public ImportResult Import(Model model, string text)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return ImportResult.Refused(new ImportDiagnostic(
                UnexpectedToken,
                "The text holds no TOML document.",
                1,
                1));
        }

        var positions = new TomlTextPositions(text);
        var document = SyntaxParser.Parse(text, "document", validate: true);

        if (document.HasErrors)
        {
            return ImportResult.Refused(Refusal(document.Diagnostics, positions));
        }

        try
        {
            var diagnostics = new List<ImportDiagnostic>();

            // One transaction for the whole document, as every importer promises: a throw while building disposes it
            // and the model is exactly as it was.
            using var transaction = model.BeginTransaction();

            var root = Build(model, document, diagnostics, positions);

            transaction.Complete();
            return ImportResult.Imported([root], diagnostics);
        }
        catch (FormatException exception)
        {
            return ImportResult.Refused(new ImportDiagnostic(UnexpectedToken, exception.Message, 1, 1));
        }
    }

    /// <summary>Fills a document node from the token stream, in the order the file has it.</summary>
    private static Node Build(
        Model model,
        DocumentSyntax document,
        List<ImportDiagnostic> diagnostics,
        TomlTextPositions positions)
    {
        var reader = TomlTokens.Of(document);
        var root = model.NewNode(TomlConceptKeys.Document);

        // The container every pair goes into: the document until a header opens a table, then that table. A header is
        // always an entry of the document, because its dotted name says where the table stands.
        var entries = root;

        while (!reader.AtEnd)
        {
            reader.SkipWhitespace();

            if (reader.AtEnd)
            {
                break;
            }

            switch (reader.Current.Kind)
            {
                case TokenKind.NewLine:
                    reader.Advance();
                    break;

                case TokenKind.Comment:
                    AddComment(model, entries, reader.Current.Text);
                    reader.Advance();
                    break;

                case TokenKind.OpenBracket:
                    reader.Advance();
                    entries = AddHeader(model, root, TomlConceptKeys.Table, TokenKind.CloseBracket, reader);
                    break;

                case TokenKind.OpenBracketDouble:
                    reader.Advance();
                    entries = AddHeader(model, root, TomlConceptKeys.ArrayOfTables, TokenKind.CloseBracketDouble, reader);
                    break;

                default:
                    ReadPair(model, entries, reader, diagnostics, positions);
                    break;
            }
        }

        return root;
    }

    /// <summary>A header node, with the name between its brackets, as an entry of the document.</summary>
    private static Node AddHeader(
        Model model,
        Node root,
        string conceptKey,
        TokenKind closer,
        TomlTokens reader)
    {
        var header = model.NewChild(root, TomlLanguage.EntriesSlot, conceptKey);
        header.Set(TomlLanguage.KeySlot, ReadKey(reader));
        reader.Expect(closer);

        return header;
    }

    /// <summary>One pair, read out of the token stream into <paramref name="entries"/>.</summary>
    private static void ReadPair(
        Model model,
        Node entries,
        TomlTokens reader,
        List<ImportDiagnostic> diagnostics,
        TomlTextPositions positions)
    {
        var pair = model.NewChild(entries, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, ReadKey(reader));

        reader.Expect(TokenKind.Equal);
        ReadValue(model, pair, TomlLanguage.ValueSlot, reader, entries, diagnostics, positions);
    }

    /// <summary>The key as it was written: dotted parts and quotes are text, never structure.</summary>
    /// <remarks>
    /// <c>a.b = 1</c> keeps <c>a.b</c> and is not resolved into a table, exactly as an XML namespace prefix keeps its
    /// text. Resolving it would move the pair on the next save, and nesting is what a header is for.
    /// </remarks>
    private static string ReadKey(TomlTokens reader)
    {
        var key = new StringBuilder();

        while (true)
        {
            key.Append(reader.ExpectKeyPart().Text);

            if (!reader.TryTake(TokenKind.Dot))
            {
                return key.ToString();
            }

            key.Append('.');
        }
    }

    /// <summary>The value at the reader, hung into <paramref name="slot"/> of <paramref name="parent"/>.</summary>
    private static Node ReadValue(
        Model model,
        Node parent,
        string slot,
        TomlTokens reader,
        Node entries,
        List<ImportDiagnostic> diagnostics,
        TomlTextPositions positions)
    {
        // A comment or a line break between the equals sign and the value is not a construct; a comment inside an
        // array is, and it has nowhere to live in this model, so it becomes an entry of the surrounding table.
        reader.SkipInsignificant(comment => MoveComment(model, entries, comment, diagnostics, positions));

        var token = reader.Current;

        switch (token.Kind)
        {
            case TokenKind.String:
                reader.Advance();
                return WithText(model, parent, slot, TomlConceptKeys.String, TomlLanguage.BasicString, token.Text, 1);

            case TokenKind.StringMulti:
                reader.Advance();
                return WithText(model, parent, slot, TomlConceptKeys.String, TomlLanguage.MultilineBasicString, token.Text, 3);

            case TokenKind.StringLiteral:
                reader.Advance();
                return WithText(model, parent, slot, TomlConceptKeys.String, TomlLanguage.LiteralString, token.Text, 1);

            case TokenKind.StringLiteralMulti:
                reader.Advance();
                return WithText(model, parent, slot, TomlConceptKeys.String, TomlLanguage.MultilineLiteralString, token.Text, 3);

            case TokenKind.Integer or TokenKind.IntegerHexa or TokenKind.IntegerOctal or TokenKind.IntegerBinary:
                reader.Advance();
                return WithLiteral(model, parent, slot, TomlConceptKeys.Integer, token.Text);

            case TokenKind.Float or TokenKind.Infinite or TokenKind.PositiveInfinite
                or TokenKind.NegativeInfinite or TokenKind.Nan or TokenKind.PositiveNan or TokenKind.NegativeNan:
                reader.Advance();
                return WithLiteral(model, parent, slot, TomlConceptKeys.Float, token.Text);

            case TokenKind.True or TokenKind.False:
                reader.Advance();
                var boolean = model.NewChild(parent, slot, TomlConceptKeys.Bool);
                boolean.Set(TomlLanguage.ValueSlot, token.Kind == TokenKind.True ? "true" : "false");
                return boolean;

            case TokenKind.OffsetDateTimeByZ or TokenKind.OffsetDateTimeByNumber:
                return WithDateTime(model, parent, slot, TomlLanguage.OffsetDateTime, reader);

            case TokenKind.LocalDateTime:
                return WithDateTime(model, parent, slot, TomlLanguage.LocalDateTime, reader);

            case TokenKind.LocalDate:
                return WithDateTime(model, parent, slot, TomlLanguage.LocalDate, reader);

            case TokenKind.LocalTime:
                return WithDateTime(model, parent, slot, TomlLanguage.LocalTime, reader);

            case TokenKind.OpenBracket:
                return ReadArray(model, parent, slot, reader, entries, diagnostics, positions);

            case TokenKind.OpenBrace:
                return ReadInlineTable(model, parent, slot, reader, entries, diagnostics, positions);

            default:
                throw new FormatException($"'{token.Text}' is not a TOML value.");
        }
    }

    /// <summary>An array's items until its closing bracket, in the order the file has them.</summary>
    private static Node ReadArray(
        Model model,
        Node parent,
        string slot,
        TomlTokens reader,
        Node entries,
        List<ImportDiagnostic> diagnostics,
        TomlTextPositions positions)
    {
        var array = model.NewChild(parent, slot, TomlConceptKeys.Array);
        reader.Expect(TokenKind.OpenBracket);

        while (true)
        {
            reader.SkipInsignificant(comment => MoveComment(model, entries, comment, diagnostics, positions));

            if (reader.TryTake(TokenKind.CloseBracket))
            {
                return array;
            }

            ReadValue(model, array, TomlLanguage.ItemsSlot, reader, entries, diagnostics, positions);

            reader.SkipInsignificant(comment => MoveComment(model, entries, comment, diagnostics, positions));

            // The comma is optional before the closing bracket, which is what makes a trailing comma legal.
            if (reader.TryTake(TokenKind.CloseBracket))
            {
                return array;
            }

            reader.Expect(TokenKind.Comma);
        }
    }

    /// <summary>An inline table's members until its closing brace, in the order the file has them.</summary>
    private static Node ReadInlineTable(
        Model model,
        Node parent,
        string slot,
        TomlTokens reader,
        Node entries,
        List<ImportDiagnostic> diagnostics,
        TomlTextPositions positions)
    {
        var table = model.NewChild(parent, slot, TomlConceptKeys.InlineTable);
        reader.Expect(TokenKind.OpenBrace);

        while (!reader.TryTake(TokenKind.CloseBrace))
        {
            var member = model.NewChild(table, TomlLanguage.MembersSlot, TomlConceptKeys.KeyValue);
            member.Set(TomlLanguage.KeySlot, ReadKey(reader));

            reader.Expect(TokenKind.Equal);
            ReadValue(model, member, TomlLanguage.ValueSlot, reader, entries, diagnostics, positions);

            if (reader.TryTake(TokenKind.CloseBrace))
            {
                return table;
            }

            reader.Expect(TokenKind.Comma);
        }

        return table;
    }

    private static Node WithText(
        Model model,
        Node parent,
        string slot,
        string conceptKey,
        string kind,
        string spelled,
        int delimiterLength)
    {
        var node = model.NewChild(parent, slot, conceptKey);

        // The delimiters are the notation's, so the slot holds what sits between them - and a multi-line string keeps
        // the line breaks and the indentation it was written with, which is the spelling a template must emit again.
        node.Set(TomlLanguage.TextSlot, Unwrap(spelled, delimiterLength));
        node.Set(TomlLanguage.KindSlot, kind);

        return node;
    }

    private static Node WithLiteral(Model model, Node parent, string slot, string conceptKey, string literal)
    {
        var node = model.NewChild(parent, slot, conceptKey);
        node.Set(TomlLanguage.TextSlot, literal);

        return node;
    }

    private static Node WithDateTime(Model model, Node parent, string slot, string kind, TomlTokens reader)
    {
        var token = reader.Current;
        reader.Advance();

        var node = model.NewChild(parent, slot, TomlConceptKeys.DateTime);
        node.Set(TomlLanguage.TextSlot, token.Text);
        node.Set(TomlLanguage.KindSlot, kind);

        return node;
    }

    /// <summary>The text between the delimiters, or the whole token when it is shorter than they are.</summary>
    private static string Unwrap(string spelled, int delimiterLength) =>
        spelled.Length >= delimiterLength * 2
            ? spelled[delimiterLength..^delimiterLength]
            : spelled;

    private static void AddComment(Model model, Node entries, string spelled)
    {
        // The hash is the notation's, so the entry holds what follows it - which keeps "# a" and "#a" different files.
        var comment = model.NewChild(entries, TomlLanguage.EntriesSlot, TomlConceptKeys.Comment);
        comment.Set(TomlLanguage.TextSlot, spelled.StartsWith('#') ? spelled[1..] : spelled);
    }

    private static void MoveComment(
        Model model,
        Node entries,
        TomlToken comment,
        List<ImportDiagnostic> diagnostics,
        TomlTextPositions positions)
    {
        AddComment(model, entries, comment.Text);

        diagnostics.Add(new ImportDiagnostic(
            CommentMoved,
            "The comment stood inside an array; it is kept as an entry of the table this pair belongs to.",
            positions.LineOf(comment.Offset),
            positions.ColumnOf(comment.Offset),
            Length: comment.Text.Length,
            Severity: ImportSeverity.Warning));
    }

    /// <summary>The refusal for text the lexer could not read.</summary>
    /// <remarks>
    /// A duplicate key gets its own code, because it is the one syntax error TOML makes where JSON merely warns - and
    /// the reader is the only thing that knows a table already defined the key. The distinction is made from the
    /// reader's message rather than from its diagnostics, which carry only a severity.
    /// </remarks>
    private static ImportDiagnostic Refusal(DiagnosticsBag diagnostics, TomlTextPositions positions)
    {
        var first = diagnostics.FirstOrDefault(diagnostic => diagnostic.Kind == DiagnosticMessageKind.Error)
            ?? diagnostics.First();

        var place = first.Span.Offset;

        return first.Message.Contains("already defined", StringComparison.Ordinal)
            ? new ImportDiagnostic(
                DuplicateKey,
                "A table defines this key twice, which TOML refuses, so nothing is imported.",
                positions.LineOf(place),
                positions.ColumnOf(place),
                Length: first.Span.Length)
            : new ImportDiagnostic(
                UnexpectedToken,
                first.Message,
                positions.LineOf(place),
                positions.ColumnOf(place),
                Length: first.Span.Length);
    }
}
