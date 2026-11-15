using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Deletes the nodes a selection covers, as one edit.
/// </summary>
/// <remarks>
/// <para>
/// This is the structural counterpart of <see cref="DeleteTextAction"/>: where a value is selected there
/// is text to remove, and where a node is selected there is a node to remove. Which nodes is
/// <see cref="SelectionRanges.NodesIn"/>'s question, so this action never decides what a range means - it
/// only asks and removes. That the answer is outermost-only is what makes several removals safe: deleting
/// a parent already takes its children with it.
/// </para>
/// <para>
/// Everything happens inside one transaction, because the user made one gesture. Deleting a list of three
/// steps therefore costs one undo, not three: the model journals each removal, and the transaction is what
/// makes the journal publish them together.
/// </para>
/// <para>
/// The caret lands where the first removed node used to be, asked before anything is removed - see
/// <see cref="DeleteNodeAction.Landing"/>. One case that helper cannot see on its own is a deletion that
/// empties the slot: it answers with a sibling, and there is none left, so the slot itself is the place
/// the next node can be created at.
/// </para>
/// </remarks>
public sealed class DeleteSelectionAction : EditAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;
        var targets = Targets(SelectionRanges.NodesIn(context));

        if (targets.Count == 0)
        {
            return EditResult.Unchanged(selection);
        }

        var (parent, slot, first) = targets[0];
        var landing = DeleteNodeAction.Landing(parent, slot, first);

        using var transaction = context.Document.Model.BeginTransaction();

        foreach (var (owner, name, node) in targets)
        {
            context.DeleteChild(owner, name, node);
        }

        if (parent.GetChildren(slot).Count == 0)
        {
            landing = Selection.At(CellAddress.OfSlot(parent.Id, slot));
        }

        transaction.Complete();

        return EditResult.Applied(landing);
    }

    /// <summary>The nodes that can actually be removed, paired with where they hang.</summary>
    /// <remarks>
    /// A node with no parent is the document root, which <see cref="SelectionRanges.NodesIn"/> never
    /// reports - the pairing is nevertheless checked rather than asserted, because a node without a place
    /// is a state the model forbids and an action must not throw on one.
    /// </remarks>
    private static List<(Node Parent, string Slot, Node Node)> Targets(IReadOnlyList<Node> nodes)
    {
        var targets = new List<(Node Parent, string Slot, Node Node)>(nodes.Count);

        foreach (var node in nodes)
        {
            if (node.Parent is { } parent && node.ParentSlot is { } slot)
            {
                targets.Add((parent, slot, node));
            }
        }

        return targets;
    }

    public override string ToString() => "delete selection";
}