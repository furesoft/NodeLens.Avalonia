using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Deletes a node together with its subtree: the one the caret stands in, or the one the caller names.
/// </summary>
/// <remarks>
/// <para>
/// A projectional editor has no characters between two cells, so the neighbour of a caret that sits on a
/// keyword is not a character but the node that keyword belongs to. That is what this action removes:
/// the node under the caret, together with its subtree, which for a filled single-valued slot means the
/// slot is emptied and for a list means one element is gone.
/// </para>
/// <para>
/// The <see cref="DeleteNodeAction(NodeId)"/> form removes the node the caller names instead of the one the
/// caret happens to be in, which is what a <see cref="DiagnosticFix"/> needs: a repair runs through the
/// session without moving the caret there first, so a gesture-addressed delete would remove whatever the user
/// happened to be looking at. It is the same operation otherwise - one action type rather than a second type
/// that would have to keep the landing rule and the root refusal in step with this one.
/// </para>
/// <para>
/// The document root is never deleted. Removing it would leave the editor with no tree at all, and
/// closing a document is the host's decision rather than a keystroke's. The named form says so with a
/// reason, because a caller that asked for a particular node is owed one.
/// </para>
/// <para>
/// The caret lands where the removed node used to be: on the sibling before it, because that sibling's
/// index does not move, or on the first remaining sibling when there was nothing before it, or on the
/// slot itself once the slot is empty - which is exactly where the next node can be created.
/// </para>
/// </remarks>
public sealed class DeleteNodeAction : EditAction
{
    private readonly NodeId _node;

    /// <summary>Deletes the node the caret stands in.</summary>
    public DeleteNodeAction()
    {
    }

    /// <summary>Deletes <paramref name="node"/> wherever the caret is.</summary>
    /// <param name="node">The node to remove, which must not be the document root.</param>
    /// <exception cref="ArgumentException"><paramref name="node"/> is empty.</exception>
    public DeleteNodeAction(NodeId node)
    {
        if (node.IsEmpty)
        {
            throw new ArgumentException("A deletion needs a node.", nameof(node));
        }

        _node = node;
    }

    /// <summary>The node this action removes, or <see cref="NodeId.Empty"/> for the caret's one.</summary>
    public NodeId Node => _node;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (_node.IsEmpty)
        {
            if (context.PlaceAt(selection.Caret) is not { } place ||
                context.NodeOf(place.Address) is not { } caret ||
                caret.Parent is not { } owner ||
                caret.ParentSlot is not { } placeOfCaret)
            {
                return EditResult.Unchanged(selection);
            }

            return Remove(context, owner, placeOfCaret, caret, selection);
        }

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this entry deletes is no longer in the document.");
        }

        if (node.Parent is not { } parent || node.ParentSlot is not { } slot)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' is the document itself, so there is nothing that could delete it.");
        }

        return Remove(context, parent, slot, node, selection);
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

    /// <summary>Removes <paramref name="node"/> and answers where the caret should land.</summary>
    /// <remarks>
    /// The landing place is asked for before the removal, because it names an index the removal renumbers.
    /// </remarks>
    private static EditResult Remove(EditContext context, Node parent, string slot, Node node, Selection selection)
    {
        var landing = Landing(parent, slot, node);

        context.DeleteChild(parent, slot, node);

        return EditResult.Applied(landing);
    }

    public override string ToString() => _node.IsEmpty ? "delete node" : $"delete {_node}";
}
