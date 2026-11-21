namespace Projectional.Projection;

/// <summary>
/// A child slot, holding the projection of whatever the slot points at.
/// </summary>
/// <remarks>
/// <para>
/// A child cell is where the recursion happens: its content is the root cell of the child's own
/// notation, so the tree mirrors the model's tree without the projection looking any different at
/// each level. The cell is also what makes an empty slot visible and clickable through
/// <see cref="EmptyText"/>, because a slot that points at nothing leaves no cell to click on
/// otherwise.
/// </para>
/// <para>
/// A slot with cardinality one holds at most one root. A slot that holds many keeps one root per child
/// in document order, running them beside each other unless the notation asked for
/// <see cref="IsStacked"/>, in which case layout starts each one on a new line. A collection with a
/// separator is a <see cref="CollectionCell"/> instead.
/// </para>
/// </remarks>
public sealed class ChildCell : StructureCell
{
    public ChildCell(CellAddress address, string slot, string emptyText, params ReadOnlySpan<EditorCell> content)
        : base(address, content)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(emptyText);

        Slot = slot;
        EmptyText = emptyText;
    }

    public string Slot { get; }

    /// <summary>What to show when the slot points at nothing, so that it can still be edited into.</summary>
    public string EmptyText { get; }

    /// <summary>Whether every child starts on a line of its own rather than beside its neighbours.</summary>
    /// <remarks>
    /// A property of the projection, not of the model: the same slot may be drawn as a column of children
    /// by one notation and as a list on one line by another. Layout is the only reader.
    /// </remarks>
    public bool IsStacked { get; init; }

    /// <summary>Whether the slot points at nothing and is showing <see cref="EmptyText"/> instead.</summary>
    public bool IsEmpty => Content.Count == 0;

    /// <summary>
    /// The positions in <see cref="EditorCell.Children"/> that get a blank line above them.
    /// </summary>
    /// <remarks>
    /// A set of indices rather than a cell, because a blank line is not a model element: a spacer in the tree would
    /// become a caret stop and a hit-test target, and there is nothing there to put a caret in. Only a stacked child
    /// cell reads it, because a gap is vertical space.
    /// <para>
    /// A gap always sits <em>above</em> a position, so the set is a range rather than only the children: position
    /// <c>0</c> is the gap above the first child and position <c>Count</c> - one past the last, which no child occupies
    /// - is the gap below it. That is what lets a list that continues into something else ask for a trailing gap
    /// without a second mechanism: the layouter asks about each child and then once about the position after the end.
    /// </para>
    /// </remarks>
    public IReadOnlySet<int> BlankLineBefore { get; init; } = NoBlankLines;

    private static IReadOnlySet<int> NoBlankLines { get; } = new HashSet<int>();

    internal override string Describe() => $"child {Slot}";
}
