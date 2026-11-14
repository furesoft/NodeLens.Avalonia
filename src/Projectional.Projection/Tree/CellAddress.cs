using Projectional.Core;

namespace Projectional.Projection;

/// <summary>
/// Where a cell sits in the model: the node it belongs to, and optionally the slot and the position
/// inside that slot's list.
/// </summary>
/// <remarks>
/// <para>
/// Addresses are the currency of everything above the tree: the caret, hit testing, completion and
/// undo all name a place by address rather than by cell object, because the tree is rebuilt on every
/// change and cell objects do not survive it. An address does: it is derived from node identity and
/// slot names only.
/// </para>
/// <para>
/// Three shapes exist, and <see cref="Slot"/> and <see cref="Index"/> tell them apart. A node
/// address (<see cref="Slot"/> is <see langword="null"/>) is carried by constant cells, spaces and
/// the layout containers in between; a slot address (<see cref="Index"/> is <c>-1</c>) by the cell
/// that shows a slot; an element address (<see cref="Index"/> is not negative) by the cell that shows
/// one element of a list. Element addresses name the <em>parent</em> node and the slot, not the
/// child, so that deleting a child shifts the addresses of the ones after it without anything having
/// to be renumbered on the child side.
/// </para>
/// <para>
/// <c>default(CellAddress)</c> is not an address: it has no owner. Structs cannot prevent it, so it is
/// documented here instead of guarded for.
/// </para>
/// </remarks>
public readonly record struct CellAddress
{
    private readonly string? _slot;

    /// <summary>The address of a whole node, used by cells that belong to no particular slot.</summary>
    public CellAddress(NodeId owner)
    {
        if (owner.IsEmpty)
        {
            throw new ArgumentException("A cell address needs a node.", nameof(owner));
        }

        Owner = owner;
        _slot = null;
        Index = -1;
    }

    private CellAddress(NodeId owner, string slot, int index)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (owner.IsEmpty)
        {
            throw new ArgumentException("A cell address needs a node.", nameof(owner));
        }

        if (index < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "An index is -1 for a whole slot.");
        }

        Owner = owner;
        _slot = slot;
        Index = index;
    }

    /// <summary>The node the cell belongs to, which is never <see cref="NodeId.Empty"/>.</summary>
    public NodeId Owner { get; }

    /// <summary>The slot the cell shows, or <see langword="null"/> for a node address.</summary>
    public string? Slot => _slot;

    /// <summary>The position inside a list slot, or <c>-1</c> for a whole slot or a node address.</summary>
    public int Index { get; }

    /// <summary>Whether this addresses a node as a whole rather than one of its slots.</summary>
    public bool IsNode => _slot is null;

    /// <summary>Whether this addresses one element of a list slot.</summary>
    public bool IsElement => Index >= 0;

    /// <summary>The address of the cell that shows slot <paramref name="slot"/> of <paramref name="owner"/>.</summary>
    public static CellAddress OfSlot(NodeId owner, string slot) => new(owner, slot, -1);

    /// <summary>The address of the cell that shows one element of a list slot.</summary>
    public static CellAddress OfElement(NodeId owner, string slot, int index) =>
        new(
            owner,
            slot,
            index >= 0
                ? index
                : throw new ArgumentOutOfRangeException(nameof(index), index, "An element index must not be negative."));

    public override string ToString() => _slot is null
        ? "<node>"
        : Index < 0 ? _slot : $"{_slot}[{Index}]";
}
