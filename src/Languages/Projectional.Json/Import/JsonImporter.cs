using System.Text;
using System.Text.Json;
using Projectional.Core;
using Projectional.Language;

namespace Projectional.Json;

/// <summary>Turns JSON text into a document of this language, once.</summary>
/// <remarks>
/// <para>
/// <b>Reading the tokens is the platform's job.</b> <see cref="Utf8JsonReader"/> owns the parts of JSON that are easy
/// to get wrong - number grammar, escape sequences, surrogate pairs - and this class owns only the mapping, which is
/// what the reference language exists to prove. Nothing here is a hand-written grammar.
/// </para>
/// <para>
/// <b>A value is copied as the file spells it</b>, not as it would be after decoding: a string keeps its <c>\n</c>, a
/// name keeps its escapes, a number keeps <c>1.0</c> instead of becoming a <c>double</c>. That is what makes
/// generation byte-exact without an escaping part in the template vocabulary, and it is why the model has no number
/// concept holding a parsed value (see <see cref="JsonLanguage"/>).
/// </para>
/// <para>
/// <b>Layout is not kept, because layout is not a construct.</b> A document is regenerated in the generator's own
/// spelling, so the golden corpus is the generator's own output - the only comparison that can be byte-exact.
/// </para>
/// <para>
/// <b>Comments are refused.</b> JSON has none, and a JSONC file accepted as if it did would come back out without
/// them - the silent rewrite the raw-text tier exists to prevent.
/// </para>
/// <para>
/// <b>A duplicate member is kept and reported.</b> JSON allows it (and discourages it), a consumer sees both, so the
/// model keeps both and a warning says so; dropping one would change the file on the next save.
/// </para>
/// </remarks>
public sealed class JsonImporter : ITextImporter
{
    /// <summary>The diagnostic for text that is not a JSON document, or does not hold exactly one value.</summary>
    public const string UnexpectedToken = "json.unexpected-token";

    /// <summary>The diagnostic for an object that names one member twice.</summary>
    public const string DuplicateName = "json.duplicate-name";

    /// <summary>The format name a host's file dialog filters on.</summary>
    public string Format => "json";

    /// <summary>Whether the text starts like a JSON value.</summary>
    /// <param name="text">The text to look at.</param>
    /// <remarks>
    /// A cheap test on the first non-blank character, never a parse: a JSON document starts with an object, an array, a
    /// string, a number or one of the three literals. The answer that matters is <see cref="Import"/>'s.
    /// </remarks>
    public bool CanImport(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character is '{' or '[' or '"' or '-' or 't' or 'f' or 'n' || char.IsAsciiDigit(character);
        }

        return false;
    }

    /// <inheritdoc/>
    public ImportResult Import(Model model, string text)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(text);

        var source = new SourcePositions(text);

        // Checked before the reader, because the tokenizer reports whitespace-only text as an error at the end of the
        // input ("line 2, column 1" for two blank lines), and "there is nothing here" is the more useful answer.
        if (string.IsNullOrWhiteSpace(text))
        {
            return ImportResult.Refused(new ImportDiagnostic(
                UnexpectedToken,
                "The text holds no JSON value.",
                1,
                1));
        }

        var reader = new Utf8JsonReader(
            source.Bytes,
            new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });

        try
        {
            reader.Read();

            var diagnostics = new List<ImportDiagnostic>();

            // Everything a host can see happens inside this transaction: a refusal or a throw below disposes it
            // without Complete, so the model is left exactly as it was and the caller needs no cleanup of its own.
            using var transaction = model.BeginTransaction();

            var root = ReadValue(model, parent: null, slot: null, ref reader, diagnostics, source);

            if (reader.Read())
            {
                // The reader validated the value, so a token after it is a second top-level value - which no JSON
                // document has, and which no file could round-trip through a model that holds one root.
                var place = source.PlaceAt(reader.TokenStartIndex);
                return ImportResult.Refused(new ImportDiagnostic(
                    UnexpectedToken,
                    "A JSON document holds exactly one value.",
                    place.Line,
                    place.Column));
            }

            transaction.Complete();
            return ImportResult.Imported([root], diagnostics);
        }
        catch (JsonException exception)
        {
            return ImportResult.Refused(Unreadable(exception, source));
        }
    }

    /// <summary>The node for the value the reader stands on, attached where the caller asked for it.</summary>
    /// <param name="model">The model being filled.</param>
    /// <param name="parent">The node to hang it into, or <see langword="null"/> for the document's root.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/>; ignored for a root.</param>
    /// <param name="reader">The reader, positioned on the value's first token.</param>
    /// <param name="diagnostics">What to report beside the result.</param>
    /// <param name="source">Where the tokens sit in the text.</param>
    private static Node ReadValue(
        Model model,
        Node? parent,
        string? slot,
        ref Utf8JsonReader reader,
        List<ImportDiagnostic> diagnostics,
        SourcePositions source) =>
        reader.TokenType switch
        {
            JsonTokenType.StartObject =>
                ReadObject(model, Create(model, parent, slot, JsonConceptKeys.Object), ref reader, diagnostics, source),

            JsonTokenType.StartArray =>
                ReadArray(model, Create(model, parent, slot, JsonConceptKeys.Array), ref reader, diagnostics, source),

            JsonTokenType.String => WithText(model, parent, slot, JsonConceptKeys.Text, reader.ValueSpan),

            JsonTokenType.Number => WithText(model, parent, slot, JsonConceptKeys.Number, reader.ValueSpan),

            JsonTokenType.True => WithBoolean(model, parent, slot, value: "true"),

            JsonTokenType.False => WithBoolean(model, parent, slot, value: "false"),

            JsonTokenType.Null => Create(model, parent, slot, JsonConceptKeys.Null),

            _ => throw new JsonException($"'{reader.TokenType}' is not a JSON value."),
        };

    /// <summary>Reads an object's members until its closing brace, in the order the file has them.</summary>
    private static Node ReadObject(
        Model model,
        Node node,
        ref Utf8JsonReader reader,
        List<ImportDiagnostic> diagnostics,
        SourcePositions source)
    {
        // Compared on the decoded name, because JSON says two members collide by value and not by spelling: "a" and
        // "\u0061" are one name written twice.
        var names = new HashSet<string>(StringComparer.Ordinal);

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var spelled = Encoding.UTF8.GetString(reader.ValueSpan);
            var name = reader.GetString() ?? string.Empty;

            if (!names.Add(name))
            {
                var place = source.PlaceAt(reader.TokenStartIndex);
                diagnostics.Add(new ImportDiagnostic(
                    DuplicateName,
                    $"The object names \"{name}\" more than once; both members are kept.",
                    place.Line,
                    place.Column,
                    Length: spelled.Length + 2,
                    Severity: ImportSeverity.Warning));
            }

            var member = model.NewChild(node, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
            member.Set(JsonLanguage.NameSlot, spelled);

            // The reader must be on the value's first token before the value is read; the name's own token has to be
            // read out first, because its text is only available while it is the current token.
            reader.Read();
            ReadValue(model, member, JsonLanguage.ValueSlot, ref reader, diagnostics, source);
        }

        return node;
    }

    /// <summary>Reads an array's items until its closing bracket, in the order the file has them.</summary>
    private static Node ReadArray(
        Model model,
        Node node,
        ref Utf8JsonReader reader,
        List<ImportDiagnostic> diagnostics,
        SourcePositions source)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            ReadValue(model, node, JsonLanguage.ItemsSlot, ref reader, diagnostics, source);
        }

        return node;
    }

    private static Node WithText(
        Model model,
        Node? parent,
        string? slot,
        string conceptKey,
        ReadOnlySpan<byte> spelled)
    {
        var node = Create(model, parent, slot, conceptKey);
        node.Set(JsonLanguage.TextSlot, Encoding.UTF8.GetString(spelled));
        return node;
    }

    private static Node WithBoolean(Model model, Node? parent, string? slot, string value)
    {
        var node = Create(model, parent, slot, JsonConceptKeys.Bool);
        node.Set(JsonLanguage.ValueSlot, value);
        return node;
    }

    /// <summary>The node of <paramref name="conceptKey"/>, as the document's root or inside its parent.</summary>
    private static Node Create(Model model, Node? parent, string? slot, string conceptKey)
    {
        if (parent is null)
        {
            return model.NewNode(conceptKey);
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);
        return model.NewChild(parent, slot, conceptKey);
    }

    /// <summary>The refusal for text the tokenizer could not read.</summary>
    /// <remarks>
    /// The place comes from the exception when it names one, so an error on the fourth line is reported there instead
    /// of at the last token that happened to parse.
    /// </remarks>
    private static ImportDiagnostic Unreadable(JsonException exception, SourcePositions source)
    {
        var offset = exception.LineNumber is { } line && exception.BytePositionInLine is { } column
            ? source.OffsetOf((int)line, (int)column)
            : 0L;

        var place = source.PlaceAt(offset);
        return new ImportDiagnostic(UnexpectedToken, Sentence(exception), place.Line, place.Column);
    }

    /// <summary>The exception's sentence without the position it appends.</summary>
    /// <remarks>
    /// <see cref="System.Text.Json"/> ends every message with <c>LineNumber: 3 | BytePositionInLine: 7.</c>. The place
    /// is a field of the diagnostic here, so repeating it as prose would put two statements of one fact in a panel.
    /// </remarks>
    private static string Sentence(JsonException exception)
    {
        var note = exception.Message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        return note < 0 ? exception.Message : exception.Message[..note];
    }
}
