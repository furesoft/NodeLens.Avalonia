namespace Projectional.Projection;

/// <summary>Collects what a layout draws, classified, instead of drawing it into a grid.</summary>
internal sealed class SnapshotRuns : ISnapshotSink
{
    private readonly List<SnapshotRun> _runs = [];

    /// <summary>The runs in the order they were drawn, which is the layout's own order.</summary>
    internal IReadOnlyList<SnapshotRun> Runs => _runs;

    /// <inheritdoc />
    public void Write(int row, int column, string text, TextStyleKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);

        _runs.Add(new SnapshotRun(row, column, text, kind));
    }
}