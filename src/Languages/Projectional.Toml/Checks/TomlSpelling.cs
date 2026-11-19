using System.Globalization;
using System.Text;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Projectional.Toml.Checks;

/// <summary>The TOML spelling rules a check needs: is this text legal, and how is it written.</summary>
/// <remarks>
/// The importer keeps the file's spelling and never decodes it, which is right for round-tripping and useless for a
/// check: a check has to ask whether a value <em>could</em> have come from a file at all. The package's lexer answers
/// that by reading the single value again, wrapped in the pair it belongs in, so the four string kinds, the number
/// grammar and the date and time kinds are not restated here.
/// </remarks>
internal static class TomlSpelling
{
    /// <summary>Whether <paramref name="literal"/> is an integer the format accepts.</summary>
    /// <param name="literal">The text a number cell holds.</param>
    internal static bool IsInteger(string literal) => ValueKindOf(literal) is
        TokenKind.Integer or TokenKind.IntegerHexa or TokenKind.IntegerOctal or TokenKind.IntegerBinary;

    /// <summary>Whether <paramref name="literal"/> is a float the format accepts.</summary>
    /// <param name="literal">The text a number cell holds.</param>
    internal static bool IsFloat(string literal) => ValueKindOf(literal) is
        TokenKind.Float or TokenKind.Infinite or TokenKind.PositiveInfinite
        or TokenKind.NegativeInfinite or TokenKind.Nan or TokenKind.PositiveNan or TokenKind.NegativeNan;

    /// <summary>Whether <paramref name="literal"/> is a date, a time or both the format accepts.</summary>
    /// <param name="literal">The text a date and time cell holds.</param>
    internal static bool IsDateTime(string literal) => ValueKindOf(literal) is
        TokenKind.OffsetDateTimeByZ or TokenKind.OffsetDateTimeByNumber
        or TokenKind.LocalDateTime or TokenKind.LocalDate or TokenKind.LocalTime;

    /// <summary>The kind of date and time <paramref name="literal"/> is, or <see langword="null"/> when it is none.</summary>
    /// <param name="literal">The text a date and time cell holds.</param>
    internal static string? DateTimeKindOf(string literal) => ValueKindOf(literal) switch
    {
        TokenKind.OffsetDateTimeByZ or TokenKind.OffsetDateTimeByNumber => TomlLanguage.OffsetDateTime,
        TokenKind.LocalDateTime => TomlLanguage.LocalDateTime,
        TokenKind.LocalDate => TomlLanguage.LocalDate,
        TokenKind.LocalTime => TomlLanguage.LocalTime,
        _ => null,
    };

    /// <summary>Whether <paramref name="text"/> is legal between the delimiters of <paramref name="kind"/>.</summary>
    /// <param name="text">The text a string cell holds.</param>
    /// <param name="kind">The spelling the cell declares.</param>
    internal static bool IsStringContent(string text, string kind)
    {
        ArgumentNullException.ThrowIfNull(text);

        var delimiter = DelimiterOf(kind);
        var expected = ValueKindOf(delimiter + text + delimiter);

        return expected == KindOf(kind);
    }

    /// <summary>The canonical spelling of <paramref name="text"/> inside a basic string.</summary>
    /// <param name="text">The value to spell, taken literally - it is not decoded first.</param>
    /// <remarks>
    /// The four short escapes are used where TOML has one, and everything else below a space becomes a
    /// <c>\uXXXX</c> escape, because a raw control character makes a hand-edited document unreadable. A newline
    /// becomes <c>\n</c> rather than staying literal, which is what keeps the repair's answer a single-line basic
    /// string whatever the broken value held.
    /// </remarks>
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
                case < ' ' or (char)0x7F:
                    spelled.Append("\\u")
                        .Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    break;
                default:
                    spelled.Append(character);
                    break;
            }
        }

        return spelled.ToString();
    }

    /// <summary>The delimiters a string of <paramref name="kind"/> is written between.</summary>
    private static string DelimiterOf(string kind) => kind switch
    {
        TomlLanguage.MultilineBasicString => "\"\"\"",
        TomlLanguage.LiteralString => "'",
        TomlLanguage.MultilineLiteralString => "'''",
        _ => "\"",
    };

    /// <summary>The token kind a string of <paramref name="kind"/> is lexed as.</summary>
    private static TokenKind KindOf(string kind) => kind switch
    {
        TomlLanguage.MultilineBasicString => TokenKind.StringMulti,
        TomlLanguage.LiteralString => TokenKind.StringLiteral,
        TomlLanguage.MultilineLiteralString => TokenKind.StringLiteralMulti,
        _ => TokenKind.String,
    };

    /// <summary>The kind of the value token in <c>value = <paramref name="literal"/></c>, or null when it is no value.</summary>
    private static TokenKind? ValueKindOf(string literal)
    {
        // A literal holding a line break would end the pair it is probed in, and a value that cannot even be written
        // in one line is not one this cell may hold.
        if (literal.Contains('\n', StringComparison.Ordinal) && !literal.Contains("\"\"\"", StringComparison.Ordinal)
            && !literal.Contains("'''", StringComparison.Ordinal))
        {
            return null;
        }

        var document = SyntaxParser.Parse("value = " + literal + "\n", "probe", validate: true);

        if (document.HasErrors)
        {
            return null;
        }

        var seenEqual = false;
        foreach (var item in document.Tokens(includeCommentsAndWhitespaces: true))
        {
            if (item is not SyntaxToken token)
            {
                continue;
            }

            switch (token.TokenKind)
            {
                case TokenKind.BasicKey when !seenEqual:
                    continue;
                case TokenKind.Equal:
                    seenEqual = true;
                    continue;
                case TokenKind.NewLine:
                    continue;
                default:
                    return seenEqual ? token.TokenKind : null;
            }
        }

        return null;
    }
}
