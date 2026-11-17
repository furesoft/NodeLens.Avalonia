using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Sorts the children of one list slot by their name.</summary>
/// <remarks>
/// <para>
/// It is addressed by owner and slot rather than by the caret, because a list is sorted as a whole: the user stands in
/// one element and means "put this list in order". The action therefore works from a caret inside any element as well
/// as from one in the slot itself, and a menu can offer it without asking where exactly the caret is.
/// </para>
/// <para>
/// Nothing is renumbered and nothing is recreated: the same nodes come out in another order, so their ids, their
/// subtrees and every reference that points into them survive (see <see cref="Model.ReorderChildren"/>). One
/// transaction, so it is one undo step.
/// </para>
/// <para>
/// The caret follows the node it was in. Element addresses name a position in the slot, so leaving the caret alone
/// would silently move it to whichever node takes that position - the user would be standing somewhere they did not
/// click.
/// </para>
/// </remarks>
public sealed class SortChildrenAction : EditAction
{
    private readonly NodeId _owner;
    private readonly string _slot;

    /// <summary>Sorts the children of <paramref name="slot"/> of <paramref name="owner"/> by name.</summary>
    /// <param name="owner">The node whose list is sorted.</param>
    /// <param name="slot">The child slot to sort, which must hold many children.</param>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is empty or <paramref name="slot"/> is not.</exception>
    public SortChildrenAction(NodeId owner, string slot)
    {
        if (owner.IsEmpty)
        {
            throw new ArgumentException("A sort needs the node whose list it sorts.", nameof(owner));
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);

        _owner = owner;
        _slot = slot;
    }

    /// <summary>The node whose list is sorted.</summary>
    public NodeId Owner => _owner;

    /// <summary>The child slot that is sorted.</summary>
    public string Slot => _slot;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.NodeOf(_owner) is not { } parent)
        {
            return EditResult.Rejected(
                selection,
                "The node whose list this sorts is no longer in the document.");
        }

        if (parent.Concept.FindChild(_slot) is not { } declared)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{parent.Concept.Key}' declares no child slot named '{_slot}'.");
        }

        if (declared.Cardinality != Cardinality.Many)
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{_slot}' of concept '{parent.Concept.Key}' holds one child, so it has no order to sort.");
        }

        if (ChildOrder.ByName(parent, _slot) is not { } order)
        {
            return EditResult.Rejected(
                selection,
                $"The children of slot '{_slot}' have no names to be sorted by.");
        }

        if (!ChildOrder.WouldChange(parent, _slot))
        {
            return EditResult.Unchanged(selection);
        }

        var followed = CaretMember(context, parent);

        context.ReorderChildren(parent, _slot, order);

        return EditResult.Applied(
            followed is null
                ? selection
                : Selection.At(CellAddress.OfElement(parent.Id, _slot, followed.IndexInParent)));
    }

    public override string ToString() => $"sort {_slot} by name";

    /// <summary>The child of <paramref name="parent"/> the caret stands in, or <see langword="null"/>.</summary>
    private Node? CaretMember(EditContext context, Node parent)
    {
        var node = context.PlaceAt(context.Selection.Caret) is { } place
            ? context.NodeOf(place.Address)
            : null;

        while (node is not null && !ReferenceEquals(node, parent))
        {
            if (ReferenceEquals(node.Parent, parent)
                && string.Equals(node.ParentSlot, _slot, StringComparison.Ordinal))
            {
                return node;
            }

            node = node.Parent;
        }

        return null;
    }
}
