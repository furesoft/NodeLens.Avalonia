using System.Text;

namespace Projectional.Hocon;

/// <summary>The lexer of the HOCON language, and the only place that knows what a token is.</summary>
/// <remarks>
/// <para>
/// Hand-written on purpose: no HOCON reader for .NET answers with a source position, and a position is exactly what
/// <see cref="ImportDiagnostic"/> promises (open question 98). The grammar this lexer serves is HOCON's own, and it is
/// small: braces, brackets, four separators, three string spellings, two comment markers and substitutions - everything
/// else is a bare run whose meaning the parser decides.
/// </para>
/// <para>
/// The lexer is total and never recovers: an unterminated string or substitution is a refusal with the place it started,
/// because a half-read token would produce a model that does not match the file.
/// </para>
/// </remarks>
internal sealed class HoconLexer
{
    private readonly string _text;
    private int _index;

    private HoconLexer(string text) => _text = text;

    /// <summary>The text the tokens came from, so the parser can slice a run it keeps verbatim.</summary>
    internal string Text => _text;

    /// <summary>Every token of <paramref name="text"/>, in order, ending with <see cref="HoconTokenKind.End"/>.</summary>
    /// <param name="text">The whole text a host passed.</param>
    /// <exception cref="FormatException">The text holds a string or a substitution that never ends.</exception>
    internal static IReadOnlyList<HoconToken> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lexer = new HoconLexer(text);
        List<HoconToken> tokens = [];

        while (true)
        {
            var token = lexer.Next();
            tokens.Add(token);

            if (token.Kind == HoconTokenKind.End)
            {
                return tokens;
            }
        }
    }

    private HoconToken Next()
    {
        while (_index < _text.Length)
        {
            var character = _text[_index];

            switch (character)
            {
                case ' ' or '\t' or '\r' or '\uFEFF':
                    _index++;
                    continue;

                case '\n':
                    _index++;
                    return new HoconToken(HoconTokenKind.NewLine, "\n", _index - 1);

                case '{':
                    return Single(HoconTokenKind.OpenBrace);

                case '}':
                    return Single(HoconTokenKind.CloseBrace);

                case '[':
                    return Single(HoconTokenKind.OpenBracket);

                case ']':
                    return Single(HoconTokenKind.CloseBracket);

                case ',':
                    return Single(HoconTokenKind.Comma);

                case ':':
                    return Single(HoconTokenKind.Colon);

                case '#':
                    return Comment(1);

                case '/' when Peek(1) == '/':
                    return Comment(2);

                case '=':
                    return Single(HoconTokenKind.Equals);

                case '+' when Peek(1) == '=':
                    _index += 2;
                    return new HoconToken(HoconTokenKind.PlusEquals, "+=", _index - 2);

                case '"':
                    return Quoted();

                case '$' when Peek(1) == '{':
                    return Substitution();

                default:
                    return Bare();
            }
        }

        return new HoconToken(HoconTokenKind.End, string.Empty, _text.Length);
    }

    /// <summary>One character that stands alone as a token.</summary>
    private HoconToken Single(HoconTokenKind kind)
    {
        var start = _index++;
        return new HoconToken(kind, _text[start].ToString(), start);
    }

    /// <summary>A note, from its marker to the end of its line.</summary>
    private HoconToken Comment(int markerLength)
    {
        var start = _index;
        _index += markerLength;

        while (_index < _text.Length && _text[_index] != '\n')
        {
            _index++;
        }

        return new HoconToken(HoconTokenKind.Comment, _text[start.._index], start);
    }

    /// <summary>A quoted or triple-quoted string, delimiters included in the token's text.</summary>
    private HoconToken Quoted()
    {
        var start = _index;

        if (Peek(1) == '"' && Peek(2) == '"')
        {
            var end = _text.IndexOf("\"\"\"", _index + 3, StringComparison.Ordinal);

            if (end < 0)
            {
                throw new FormatException($"The multi-line string that starts at {start} never ends.");
            }

            _index = end + 3;
            return new HoconToken(HoconTokenKind.TripleQuoted, _text[start.._index], start);
        }

        _index++;

        while (_index < _text.Length)
        {
            var character = _text[_index];

            if (character == '\\')
            {
                _index += 2;
                continue;
            }

            if (character == '"')
            {
                _index++;
                return new HoconToken(HoconTokenKind.Quoted, _text[start.._index], start);
            }

            if (character == '\n')
            {
                break;
            }

            _index++;
        }

        throw new FormatException($"The string that starts at {start} never ends.");
    }

    /// <summary>A whole substitution, kept as written because nothing inside it is HOCON.</summary>
    private HoconToken Substitution()
    {
        var start = _index;
        var end = _text.IndexOf('}', _index + 2);

        if (end < 0)
        {
            throw new FormatException($"The substitution that starts at {start} never ends.");
        }

        _index = end + 1;
        return new HoconToken(HoconTokenKind.Substitution, _text[start.._index], start);
    }

    /// <summary>An unquoted run: everything up to the next character that means something else.</summary>
    private HoconToken Bare()
    {
        var start = _index;
        var run = new StringBuilder();

        while (_index < _text.Length)
        {
            var character = _text[_index];

            if (char.IsWhiteSpace(character)
                || character is '{' or '}' or '[' or ']' or ',' or ':' or '=' or '#' or '"'
                || (character == '/' && Peek(1) == '/')
                || (character == '$' && Peek(1) == '{')
                || (character == '+' && Peek(1) == '='))
            {
                break;
            }

            run.Append(character);
            _index++;
        }

        return new HoconToken(HoconTokenKind.Bare, run.ToString(), start);
    }

    private char Peek(int ahead) => _index + ahead < _text.Length ? _text[_index + ahead] : '\0';
}
