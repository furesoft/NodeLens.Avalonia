using System.Globalization;
using System.Text;

namespace Projectional.LanguageExport;

/// <summary>Builds C# source text: indentation, line ends and the one escaping rule an export needs.</summary>
/// <remarks>
/// A writer rather than string concatenation at each call site, because the shape of generated code is entirely
/// nesting - an array inside an argument list inside a class - and every level has to indent the same way. The line
/// ending is <c>\n</c> on purpose, like every file this repository writes: a generated file that differed by line
/// ending between machines would be a diff nobody made.
/// </remarks>
internal sealed class SourceWriter
{
    private const string IndentText = "    ";

    private readonly StringBuilder _text = new();
    private int _depth;

    /// <summary>Writes <paramref name="text"/> at no indentation and appends a line ending.</summary>
    public void Line(string text = "")
    {
        if (text.Length > 0)
        {
            _text.Append(' ', _depth * IndentText.Length).Append(text);
        }

        _text.Append('\n');
    }

    /// <summary>Writes one level of indentation and a line ending, which is what a blank line costs.</summary>
    public void Blank() => _text.Append('\n');

    /// <summary>One level deeper until the returned value is disposed.</summary>
    public IndentedScope Indent()
    {
        _depth++;

        return new IndentedScope(this);
    }

    /// <summary>A C# string literal for <paramref name="text"/>, or <c>null</c> for a null.</summary>
    /// <remarks>
    /// Everything outside printable ASCII is escaped, because the generated file must not depend on the encoding a
    /// tool happened to write it in - and a language key or a description may hold anything a model accepts.
    /// </remarks>
    public static string Literal(string? text)
    {
        if (text is null)
        {
            return "null";
        }

        var literal = new StringBuilder(text.Length + 2);
        literal.Append('"');

        foreach (var character in text)
        {
            switch (character)
            {
                case '"':
                    literal.Append("\\\"");
                    break;
                case '\\':
                    literal.Append("\\\\");
                    break;
                case '\n':
                    literal.Append("\\n");
                    break;
                case '\r':
                    literal.Append("\\r");
                    break;
                case '\t':
                    literal.Append("\\t");
                    break;
                default:
                    if (character is < ' ' or > '~')
                    {
                        literal.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        literal.Append(character);
                    }

                    break;
            }
        }

        return literal.Append('"').ToString();
    }

    public override string ToString() => _text.ToString();

    /// <summary>The half of <see cref="Indent"/> that closes the scope.</summary>
    internal readonly struct IndentedScope : IDisposable
    {
        private readonly SourceWriter _writer;

        public IndentedScope(SourceWriter writer) => _writer = writer;

        public void Dispose() => _writer._depth--;
    }
}
