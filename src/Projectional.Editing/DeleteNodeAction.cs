using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Deletes the node the caret stands in, or empties the slot it fills.
/// </summary>
/// <remarks>
/// <para>
/// A projectional editor has no characters between two cells, so the neighbour of a caret that sits on a
/// keyword is not a character but the node that keyword belongs to. That is what this action removes:
/// the node under the caret, together with its subtree, which for a filled single-valued slot means the
/// slot is emptied and for a list means one element is gone.
/// </para>
/// <para>
/// The document root is never deleted. Removing it would leave the editor with no tree at all, and
/// closing a document is the host's decision rather than a keystroke's.
/// </para>
/// <para>
/// The caret lands where the removed node used to be: on the sibling before it, because that sibling's
/// index does not move, or on the first remaining sibling when there was nothing before it, or on the
/// slot itself once the slot is empty - which is exactly where the next node can be created.
/// </para>
/// </remarks>
public sealed class DeleteNodeAction : EditAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.PlaceAt(selection.Caret) is not { } place ||
            context.NodeOf(place.Address) is not { } node ||
            node.Parent is not { } parent ||
            node.ParentSlot is not { } slot)
        {
            return EditResult.Unchanged(selection);
        }

        var landing = Landing(parent, slot, node);

        context.DeleteChild(parent, slot, node);

        return EditResult.Applied(landing);
    }

    /// <summary>Where the caret goes once <paramref name="node"/> is gone.</summary>
    /// <remarks>
    /// <para>
    /// Element addresses name the parent, so deleting a child renumbers the ones after it without
    /// touching the child itself. The sibling before the deleted node keeps its index; the sibling after
    /// it moves into the deleted node's index. Landing on the earlier one therefore needs no arithmetic,
    /// and landing on the later one is index <c>0</c> in the degenerate case where the deleted node was
    /// first.
    /// </para>
    /// <para>
    /// Shared with <see cref="DeleteSelectionAction"/>, which deletes several nodes at once and lands on
    /// the place the first of them came from - the same question, asked before any of them is removed.
    /// </para>
    /// </remarks>
    internal static Selection Landing(Node parent, string slot, Node node)
    {
        var index = node.IndexInParent;

        if (index > 0)
        {
            return Selection.At(CellAddress.OfElement(parent.Id, slot, index - 1));
        }

        return parent.GetChildren(slot).Count > 1
            ? Selection.At(CellAddress.OfElement(parent.Id, slot, 0))
            : Selection.At(CellAddress.OfSlot(parent.Id, slot));
    }

    public override string ToString() => "delete node";
}
