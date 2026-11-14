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
/// A slot with cardinality one holds at most one root. Slots that hold many children are projected
/// by <see cref="CollectionCell"/>, which shows the separator and the empty text that a collection
/// needs and a single child does not.
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

    /// <summary>Whether the slot points at nothing and is showing <see cref="EmptyText"/> instead.</summary>
    public bool IsEmpty => Content.Count == 0;

    internal override string Describe() => $"child {Slot}";
}
