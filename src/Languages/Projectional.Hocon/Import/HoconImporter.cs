using System.Globalization;
using Projectional.Core;
using Projectional.Language;

namespace Projectional.Hocon;

/// <summary>Turns HOCON text into a document of this language, once.</summary>
/// <remarks>
/// <para>
/// <b>Nothing is evaluated.</b> A substitution stays a substitution, an <c>include</c> stays an include, and an
/// <c>+=</c> stays the operator it was written as - the model owns the text, not the configuration that text would
/// resolve to. The framework never follows an include: import takes one string from the host, so reading another file
/// would be both a purity break and the same class of hazard XML's refused external entities exist to prevent.
/// </para>
/// <para>
/// <b>Repeated keys are kept, in order.</b> HOCON merges them when a reader resolves the file; the text has two, and
/// dropping one would rewrite the user's file - the opposite of the TOML decision, and it follows from the two formats.
/// </para>
/// <para>
/// <b>A spelling is kept.</b> The separator a property was written with, the marker that introduced a note and which of
/// the three spellings a string is are all slots, so a file the user wrote comes back out as that file.
/// </para>
/// </remarks>
public sealed class HoconImporter : ITextImporter
{
    /// <summary>The diagnostic for text that is not a HOCON document.</summary>
    public const string UnexpectedToken = "hocon.unexpected-token";

    /// <summary>The format name a host's file dialog filters on.</summary>
    public string Format => "hocon";

    /// <summary>Whether the text starts like a HOCON document.</summary>
    /// <param name="text">The text to look at.</param>
    /// <remarks>
    /// A cheap shape question on the first meaningful line: HOCON is JSON's superset, so a brace answers, and so does a
    /// pair with one of its four separators. A colon answers as readily as an equals sign, which is what makes this
    /// format's hint the weakest of the three - the answer that matters is <see cref="Import"/>'s.
    /// </remarks>
    public bool CanImport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed[0] == '{')
            {
                return true;
            }

            var equals = trimmed.IndexOf('=', StringComparison.Ordinal);
            var colon = trimmed.IndexOf(':', StringComparison.Ordinal);

            // The separator that comes first is the one this line uses; the other may still appear inside the value, as it
            // does in `a: { b = 1 }` - a colon-separated property holding an object written with equals signs. Asking for
            // one of the two to be absent made that line look like no HOCON at all.
            return (colon > 0 && (equals < 0 || colon < equals)) || (equals > 0 && (colon < 0 || equals < colon));
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
                "The text holds no HOCON document.",
                1,
                1));
        }

        var positions = new HoconTextPositions(text);

        try
        {
            var reader = new HoconTokens(HoconLexer.Tokenize(text));

            // One transaction for the whole document: a throw below disposes it and the model is exactly as it was.
            using var transaction = model.BeginTransaction();

            var document = model.NewNode(HoconConceptKeys.Document);
            ReadMembers(model, document, reader, text, insideBraces: false);

            transaction.Complete();
            return ImportResult.Imported([document]);
        }
        catch (HoconSyntaxException exception)
        {
            return ImportResult.Refused(Refusal(exception, positions));
        }
        catch (FormatException exception)
        {
            return ImportResult.Refused(new ImportDiagnostic(UnexpectedToken, exception.Message, 1, 1));
        }
    }

    /// <summary>Reads members until the closing brace or the end of the text.</summary>
    private static void ReadMembers(
        Model model,
        Node container,
        HoconTokens reader,
        string text,
        bool insideBraces)
    {
        while (true)
        {
            reader.SkipNewLines();

            if (reader.AtEnd)
            {
                if (insideBraces)
                {
                    throw new HoconSyntaxException("The object that starts here never closes.", reader.Current.Offset);
                }

                return;
            }

            switch (reader.Current.Kind)
            {
                case HoconTokenKind.CloseBrace when insideBraces:
                    reader.Advance();
                    return;

                case HoconTokenKind.Comment:
                    AddComment(model, container, reader.Current);
                    reader.Advance();
                    continue;

                case HoconTokenKind.Bare when string.Equals(reader.Current.Text, "include", StringComparison.Ordinal):
                    AddInclude(model, container, reader, text);
                    continue;

                case HoconTokenKind.CloseBrace or HoconTokenKind.CloseBracket:
                    throw new HoconSyntaxException(
                        $"'{reader.Current.Text}' closes nothing here.",
                        reader.Current.Offset);

                default:
                    ReadProperty(model, container, reader, text);
                    break;
            }
        }
    }

    /// <summary>One property: a key, the separator it was written with, and its value.</summary>
    private static void ReadProperty(Model model, Node container, HoconTokens reader, string text)
    {
        var property = model.NewChild(container, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        property.Set(HoconLanguage.KeySlot, ReadKey(reader));

        var separator = reader.Current.Kind switch
        {
            HoconTokenKind.Equals => HoconLanguage.EqualsOperator,
            HoconTokenKind.Colon => HoconLanguage.ColonOperator,
            HoconTokenKind.PlusEquals => HoconLanguage.AppendOperator,
            _ => HoconLanguage.SpaceOperator,
        };

        if (reader.Current.Kind is HoconTokenKind.Equals or HoconTokenKind.Colon or HoconTokenKind.PlusEquals)
        {
            reader.Advance();
        }

        property.Set(HoconLanguage.OperatorSlot, separator);
        ReadValue(model, property, HoconLanguage.ValueSlot, reader, text);
    }

    /// <summary>The key as it was written, quotes and dots included.</summary>
    /// <remarks>
    /// A dotted path, a quoted key and an unquoted one are three spellings of one path, and the model keeps the spelling
    /// it was given: resolving the dots would rewrite the file, and dropping the quotes would lose the reason a key needed
    /// them.
    /// </remarks>
    private static string ReadKey(HoconTokens reader)
    {
        var token = reader.Current;

        switch (token.Kind)
        {
            case HoconTokenKind.Quoted or HoconTokenKind.TripleQuoted or HoconTokenKind.Bare:
                reader.Advance();
                return token.Text;

            default:
                throw new HoconSyntaxException($"'{token.Text}' cannot stand where a key belongs.", token.Offset);
        }
    }

    /// <summary>A value, which is a concatenation when more than one thing is written next to another.</summary>
    /// <remarks>
    /// Juxtaposition is meaningful in HOCON (<c>greeting = hello ${name}</c>), so flattening it into one string would lose
    /// the substitution inside it. The concatenation is built first and unwrapped again when only one part arrived:
    /// <see cref="Model.Unwrap"/> is the primitive for "what is inside takes my place", and a one-part concatenation is
    /// not a construct the file had.
    /// </remarks>
    private static Node ReadValue(Model model, Node parent, string slot, HoconTokens reader, string text)
    {
        var concatenation = model.NewChild(parent, slot, HoconConceptKeys.Concat);

        ReadUnit(model, concatenation, HoconLanguage.PartsSlot, reader, text);

        var parts = 1;
        while (reader.AtValueStart())
        {
            ReadUnit(model, concatenation, HoconLanguage.PartsSlot, reader, text);
            parts++;
        }

        return parts == 1 ? model.Unwrap(concatenation, HoconLanguage.PartsSlot) : concatenation;
    }

    /// <summary>One thing: an object, an array, a substitution, a string, a number, a literal or null.</summary>
    private static Node ReadUnit(Model model, Node parent, string slot, HoconTokens reader, string text)
    {
        var token = reader.Current;

        switch (token.Kind)
        {
            case HoconTokenKind.OpenBrace:
                reader.Advance();
                var container = model.NewChild(parent, slot, HoconConceptKeys.Object);
                ReadMembers(model, container, reader, text, insideBraces: true);
                return container;

            case HoconTokenKind.OpenBracket:
                return ReadArray(model, parent, slot, reader, text);

            case HoconTokenKind.Substitution:
                reader.Advance();
                return ReadSubstitution(model, parent, slot, token);

            case HoconTokenKind.Quoted or HoconTokenKind.TripleQuoted:
                reader.Advance();
                var string_ = model.NewChild(parent, slot, HoconConceptKeys.String);
                string_.Set(HoconLanguage.TextSlot, Unquote(token));
                string_.Set(
                    HoconLanguage.QuotedSlot,
                    token.Kind == HoconTokenKind.TripleQuoted ? HoconLanguage.TripleQuoted : HoconLanguage.Quoted);
                return string_;

            case HoconTokenKind.Bare:
                reader.Advance();
                return FromBare(model, parent, slot, token.Text);

            default:
                throw new HoconSyntaxException($"'{token.Text}' cannot stand where a value belongs.", token.Offset);
        }
    }

    /// <summary>An array's items until its closing bracket, in the order the file has them.</summary>
    private static Node ReadArray(Model model, Node parent, string slot, HoconTokens reader, string text)
    {
        var array = model.NewChild(parent, slot, HoconConceptKeys.Array);
        reader.Advance();

        while (true)
        {
            reader.SkipNewLines();

            if (reader.AtEnd)
            {
                throw new HoconSyntaxException("The array that starts here never closes.", reader.Current.Offset);
            }

            if (reader.Current.Kind == HoconTokenKind.CloseBracket)
            {
                reader.Advance();
                return array;
            }

            ReadValue(model, array, HoconLanguage.ItemsSlot, reader, text);

            // The comma is optional: HOCON lets a line break separate two items.
            if (reader.Current.Kind == HoconTokenKind.Comma)
            {
                reader.Advance();
            }
        }
    }

    /// <summary>A substitution, split into its path and whether it may resolve to nothing.</summary>
    private static Node ReadSubstitution(Model model, Node parent, string slot, HoconToken token)
    {
        // The raw text is ${path} or ${?path}; the delimiters are the notation's, so only the path is stored.
        var inner = token.Text[2..^1];
        var optional = inner.StartsWith('?');

        var substitution = model.NewChild(parent, slot, HoconConceptKeys.Substitution);
        substitution.Set(HoconLanguage.PathSlot, optional ? inner[1..] : inner);
        substitution.Set(
            HoconLanguage.OptionalSlot,
            optional ? HoconLanguage.Optional : HoconLanguage.Required);

        return substitution;
    }

    /// <summary>The node for an unquoted run, classified by what it says.</summary>
    private static Node FromBare(Model model, Node parent, string slot, string text)
    {
        if (text is "true" or "false")
        {
            var boolean = model.NewChild(parent, slot, HoconConceptKeys.Bool);
            boolean.Set(HoconLanguage.ValueSlot, text);
            return boolean;
        }

        if (string.Equals(text, "null", StringComparison.Ordinal))
        {
            return model.NewChild(parent, slot, HoconConceptKeys.Null);
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            var number = model.NewChild(parent, slot, HoconConceptKeys.Number);
            number.Set(HoconLanguage.LiteralSlot, text);
            return number;
        }

        var unquoted = model.NewChild(parent, slot, HoconConceptKeys.String);
        unquoted.Set(HoconLanguage.TextSlot, text);
        unquoted.Set(HoconLanguage.QuotedSlot, HoconLanguage.Unquoted);

        return unquoted;
    }

    /// <summary>A note, split into the marker it was written with and what it says.</summary>
    private static void AddComment(Model model, Node container, HoconToken token)
    {
        var marker = token.Text.StartsWith("//", StringComparison.Ordinal) ? "//" : "#";

        var comment = model.NewChild(container, HoconLanguage.MembersSlot, HoconConceptKeys.Comment);
        comment.Set(HoconLanguage.MarkerSlot, marker);
        comment.Set(HoconLanguage.TextSlot, token.Text[marker.Length..]);
    }

    /// <summary>An include, kept as the text that follows it and never followed.</summary>
    private static void AddInclude(Model model, Node container, HoconTokens reader, string text)
    {
        var token = reader.Current;

        // The target is the rest of the line, taken from the file so `file("x")` and `"x"` stay what they were: a token
        // stream cannot reproduce the spaces between them, and this is a run the model does not interpret.
        var start = token.Offset + token.Text.Length;
        var end = start;
        while (end < text.Length && text[end] != '\n')
        {
            end++;
        }

        var include = model.NewChild(container, HoconLanguage.MembersSlot, HoconConceptKeys.Include);
        include.Set(HoconLanguage.TargetSlot, text[start..end].Trim());

        // Step over every token of that line, so the member loop resumes after it.
        reader.Advance();
        while (!reader.AtEnd && reader.Current.Kind != HoconTokenKind.NewLine && reader.Current.Offset < end)
        {
            reader.Advance();
        }
    }

    /// <summary>The text between a string's delimiters, escapes included.</summary>
    private static string Unquote(HoconToken token)
    {
        var delimiters = token.Kind == HoconTokenKind.TripleQuoted ? 3 : 1;

        return token.Text.Length >= delimiters * 2 ? token.Text[delimiters..^delimiters] : token.Text;
    }

    /// <summary>The refusal for a place the parser stopped at.</summary>
    private static ImportDiagnostic Refusal(HoconSyntaxException exception, HoconTextPositions positions) =>
        new(UnexpectedToken, exception.Message, positions.LineOf(exception.Offset), positions.ColumnOf(exception.Offset));
}
