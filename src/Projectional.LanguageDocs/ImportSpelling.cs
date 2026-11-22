using System.Globalization;
using System.Text;

using Projectional.ImportRules;

namespace Projectional.LanguageDocs;

/// <summary>One part of a declared importer's rule, or one of its token classes, written as the language draws it.</summary>
/// <remarks>
/// <para>
/// The words are the notation's own - <c>capture name from word</c>, <c>repeat stages separated by ","</c>,
/// <c>must be tag again</c> - because a reader who opens the importer document in the editor has to meet the same
/// sentence here. A second vocabulary for one declaration would be a second thing to learn, and the page would stop
/// being a description of what the language says and become a description of what this file happens to print.
/// </para>
/// <para>
/// Two things are deliberately left quieter than the notation: a repeat with no separator omits "separated by", and one
/// with no minimum omits "at least", because drawing the empty cell would put <c>""</c> and <c>0</c> in front of a
/// reader as if they were text. The notation has to show every property of a node; a page may say only what the
/// declaration means.
/// </para>
/// </remarks>
internal static class ImportSpelling
{
    /// <summary>One part, in the words the import language draws.</summary>
    internal static string Of(ImportPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.Kind switch
        {
            ImportPartKind.Literal => Quote(part.Text),
            ImportPartKind.Capture => part.Token.Length == 0
                ? $"capture {part.Slot}"
                : $"capture {part.Slot} from {part.Token}",
            ImportPartKind.Line => $"line {part.Slot}",
            ImportPartKind.Nest => $"nest {part.Slot}",
            ImportPartKind.Repeat => Repeat(part),
            ImportPartKind.Raw => $"raw {part.Slot} until {Quote(part.Text)}",
            ImportPartKind.Bind => part.Text.Length == 0
                ? $"bind {part.Name}"
                : $"bind {part.Name} from {part.Text}",
            ImportPartKind.Matches => $"must be {part.Name} again",
            ImportPartKind.Fail => $"fail {part.Text} {Quote(part.Message)}",
            var other => other.ToString(),
        };
    }

    /// <summary>One token class, in the words the import language draws.</summary>
    internal static string Of(ImportToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return token.Kind switch
        {
            ImportTokenKind.Word => token.Extra.Length == 0
                ? $"token {token.Name} is a word"
                : $"token {token.Name} is a word, and also accepts {Quote(token.Extra)}",
            ImportTokenKind.Number => $"token {token.Name} is a number",
            ImportTokenKind.Quoted => token.Escape.Length == 0
                ? $"token {token.Name} stands between {Mark(token.Quote)} and resolves nothing"
                : $"token {token.Name} stands between {Mark(token.Quote)} and resolves {Mark(token.Escape)}",
            ImportTokenKind.Fixed => $"token {token.Name} stands between {Mark(token.Extra)} and {Mark(token.Quote)} resolving nothing",
            _ => token.Prefix.Length == 0
                ? $"token {token.Name} is skipped as whitespace"
                : $"token {token.Name} is skipped, starting at {Quote(token.Prefix)} until {Quote(token.Until)}",
        };
    }

    /// <summary>Every part of <paramref name="rule"/>, one per line, in the order the text has them.</summary>
    /// <remarks>
    /// One part per line because a rule is a sequence: the notation stacks them for the same reason, and a reader
    /// scanning a cell that reads <c>"stage = " / line command</c> sees the two halves of the shape.
    /// </remarks>
    internal static string Of(ImportRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return string.Join('\n', rule.Parts.Select(Of));
    }

    private static string Repeat(ImportPart part)
    {
        var text = new StringBuilder($"repeat {part.Slot}");

        if (part.Separator.Length > 0)
        {
            text.Append(" separated by ").Append(Quote(part.Separator));
        }

        if (part.Minimum > 0)
        {
            text.Append(" at least ").Append(part.Minimum.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>Text as one line in quotes, so a line break or a tab cannot reshape what a reader sees.</summary>
    private static string Quote(string text) =>
        "\""
        + text.Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
        + "\"";

    /// <summary>One mark in single quotes, which is what tells a lone <c>"</c> apart from a missing quotation.</summary>
    private static string Mark(string text) =>
        "'"
        + text.Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
        + "'";
}
