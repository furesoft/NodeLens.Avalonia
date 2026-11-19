namespace Projectional.Yaml;

/// <summary>The one translation between an offset in the text and the place a reader sees.</summary>
/// <remarks>
/// The tokenizer reports offsets in characters of the string a host passed, and a diagnostic promises a line and a column.
/// Keeping that conversion in one place is what makes a refusal and a warning measured the same way.
/// </remarks>
internal sealed class YamlTextPositions
{
    private readonly string _text;
    private readonly int[] _lineStarts;

    internal YamlTextPositions(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _text = text;

        List<int> starts = [0];
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        _lineStarts = [.. starts];
    }

    /// <summary>The 1-based line of <paramref name="offset"/>.</summary>
    /// <param name="offset">A character offset the tokenizer reported; one past the end is clamped to it.</param>
    internal int LineOf(int offset) => LineIndexOf(offset) + 1;

    /// <summary>The 1-based column of <paramref name="offset"/>.</summary>
    /// <param name="offset">A character offset the tokenizer reported; one past the end is clamped to it.</param>
    internal int ColumnOf(int offset) =>
        Math.Clamp(offset, 0, _text.Length) - _lineStarts[LineIndexOf(offset)] + 1;

    private int LineIndexOf(int offset)
    {
        var clamped = Math.Clamp(offset, 0, _text.Length);

        var line = 0;
        while (line + 1 < _lineStarts.Length && _lineStarts[line + 1] <= clamped)
        {
            line++;
        }

        return line;
    }
}
