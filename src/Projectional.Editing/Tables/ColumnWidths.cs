using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The widths a reader has dragged a table's columns to, keyed by the table and the column.
/// </summary>
/// <remarks>
/// <para>
/// This is view state, exactly like <see cref="INotationChoice"/> and the fold set: it is never saved,
/// it creates no undo step, and it is keyed by the stable node id so it survives every rebuild and an
/// undo that moves nodes around. Two readers of one model may disagree about it, which is why it is
/// not part of the document.
/// </para>
/// <para>
/// The value is immutable and every change answers a new one, so a caller can hold on to a snapshot
/// without the session mutating it underneath.
/// </para>
/// </remarks>
public sealed class ColumnWidths : IColumnWidths
{
    private readonly Dictionary<(NodeId Owner, string Slot, int Column), double> _widths;

    private ColumnWidths(Dictionary<(NodeId Owner, string Slot, int Column), double> widths) => _widths = widths;

    /// <summary>No column has been resized, so every column is as wide as its content.</summary>
    public static ColumnWidths Empty { get; } = new([]);

    /// <summary>Whether the reader has resized anything at all.</summary>
    public bool IsEmpty => _widths.Count == 0;

    /// <inheritdoc />
    public double? WidthOf(NodeId owner, string slot, int column) =>
        _widths.TryGetValue((owner, slot, column), out var width) ? width : null;

    /// <summary>The same widths with one column set to <paramref name="width"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The width is not a positive, finite number of layout units.
    /// </exception>
    public ColumnWidths With(NodeId owner, string slot, int column, double width)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "A column width has to be a positive, finite number of layout units.");
        }

        var widths = new Dictionary<(NodeId Owner, string Slot, int Column), double>(_widths)
        {
            [(owner, slot, column)] = width,
        };

        return new ColumnWidths(widths);
    }

    /// <summary>The same widths with one column back to its content's width.</summary>
    public ColumnWidths Without(NodeId owner, string slot, int column)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (!_widths.ContainsKey((owner, slot, column)))
        {
            return this;
        }

        var widths = new Dictionary<(NodeId Owner, string Slot, int Column), double>(_widths);
        widths.Remove((owner, slot, column));

        return new ColumnWidths(widths);
    }

    /// <summary>The same widths with every column of <paramref name="owner"/> forgotten.</summary>
    /// <remarks>
    /// A deleted node's widths would otherwise stay in the map for the life of the session. Nothing
    /// breaks if they do - a new node has a new id - but a session that is open for hours should not
    /// remember tables that no longer exist.
    /// </remarks>
    public ColumnWidths Without(NodeId owner)
    {
        var widths = new Dictionary<(NodeId Owner, string Slot, int Column), double>(_widths);

        foreach (var key in _widths.Keys)
        {
            if (key.Owner == owner)
            {
                widths.Remove(key);
            }
        }

        return widths.Count == _widths.Count ? this : new ColumnWidths(widths);
    }

    /// <inheritdoc />
    public override string ToString() =>
        _widths.Count == 0
            ? "no column widths"
            : string.Join(", ", _widths.Select(entry => $"{entry.Key.Slot}[{entry.Key.Column}]={entry.Value}"));
}
