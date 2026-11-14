namespace Projectional.Projection;

/// <summary>
/// A fixed grid of characters that a layout is drawn into, so that a rendering can be asserted on
/// as text without a window and without a screenshot.
/// </summary>
/// <remarks>
/// <para>
/// The grid refuses to draw past its right edge instead of clipping, because a layout test that
/// silently loses the end of a line passes for the wrong reason. Callers that want a narrower
/// picture clip the text themselves, which keeps the decision where the width is chosen.
/// </para>
/// <para>
/// <see cref="ToString"/> trims trailing blanks from every line and drops trailing blank lines, so
/// that a snapshot shows what was drawn rather than how much room was reserved for it.
/// </para>
/// </remarks>
internal sealed class SnapshotGrid
{
    private readonly char[][] _rows;

    internal SnapshotGrid(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        _rows = new char[height][];

        for (var row = 0; row < height; row++)
        {
            _rows[row] = new string(' ', width).ToCharArray();
        }
    }

    internal int Width { get; }

    internal int Height { get; }

    /// <summary>Draws <paramref name="text"/> at <paramref name="row"/> and <paramref name="column"/>.</summary>
    /// <exception cref="ArgumentException">The text would leave the grid.</exception>
    internal void Write(int row, int column, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        if (row >= Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(row),
                row,
                $"The grid has {Height} rows, numbered from zero.");
        }

        if (column + text.Length > Width)
        {
            throw new ArgumentException(
                $"The text \"{text}\" does not fit: it needs {text.Length} columns from {column}, "
                + $"and the grid is {Width} columns wide.",
                nameof(text));
        }

        text.CopyTo(0, _rows[row], column, text.Length);
    }

    /// <summary>What was drawn, with no trailing blanks and no trailing blank lines.</summary>
    public override string ToString()
    {
        var lines = new List<string>(Height);

        foreach (var row in _rows)
        {
            lines.Add(new string(row).TrimEnd());
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }
}
