using System.Globalization;
using System.Text;

namespace Projectional.Hocon.Checks;

/// <summary>The HOCON spelling rules a check needs: is this run legal unquoted, and how is it written between quotes.</summary>
/// <remarks>
/// The importer keeps the file's spelling and never decodes it, which is right for round-tripping and useless for a
/// check: a check has to ask whether a value <em>could</em> have come from a file at all. The rules are the lexer's own -
/// a run ends at whitespace and at the eight characters that mean something else - restated here because a check cannot
/// re-lex a model.
/// </remarks>
internal static class HoconSpelling
{
    /// <summary>The characters that end an unquoted run.</summary>
    private const string Boundary = "{}[],:=+#\"/";

    /// <summary>Whether <paramref name="text"/> can stand unquoted in a file.</summary>
    /// <param name="text">The text an unquoted string cell holds.</param>
    internal static bool IsLegalRun(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Length > 0
            && !text.Any(character => char.IsWhiteSpace(character) || Boundary.Contains(character, StringComparison.Ordinal))
            && !text.StartsWith("${", StringComparison.Ordinal);
    }

    /// <summary>Whether <paramref name="literal"/> is a number the format accepts.</summary>
    /// <param name="literal">The text a number cell holds.</param>
    internal static bool IsNumber(string literal)
    {
        ArgumentNullException.ThrowIfNull(literal);

        return double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            && IsLegalRun(literal);
    }

    /// <summary>The canonical spelling of <paramref name="text"/> inside a double-quoted string.</summary>
    /// <param name="text">The value to spell, taken literally - it is not decoded first.</param>
    internal static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var spelled = new StringBuilder(text.Length + 2);
        foreach (var character in text)
        {
            switch (character)
            {
                case '"':
                    spelled.Append("\\\"");
                    break;
                case '\\':
                    spelled.Append("\\\\");
                    break;
                case '\b':
                    spelled.Append("\\b");
                    break;
                case '\f':
                    spelled.Append("\\f");
                    break;
                case '\n':
                    spelled.Append("\\n");
                    break;
                case '\r':
                    spelled.Append("\\r");
                    break;
                case '\t':
                    spelled.Append("\\t");
                    break;
                default:
                    spelled.Append(character);
                    break;
            }
        }

        return spelled.ToString();
    }
}
