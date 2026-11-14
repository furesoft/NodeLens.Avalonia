namespace Projectional.Language;

/// <summary>
/// One horizontal line of inline cells. The line is the unit of vertical layout, which is why text
/// does not wrap unless a cell opts in.
/// </summary>
public sealed class LineNotationCell : BlockNotationCell
{
    public LineNotationCell(params ReadOnlySpan<InlineNotationCell> content)
    {
        Content = [.. content];
    }

    public IReadOnlyList<InlineNotationCell> Content { get; }

    public override string ToString() => $"line({Content.Count})";
}
