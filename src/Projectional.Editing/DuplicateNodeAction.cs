using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Copies a node and everything below it, right after the node itself.</summary>
/// <remarks>
/// <para>
/// The copy is a new subtree with new identities (<see cref="Model.InsertCopy"/>), so the two can be edited apart
/// afterwards - which is the whole reason to duplicate something. It is addressed by node id rather than by the
/// caret, because a repair or an intention names what it acts on, and the place is the source's own: the next
/// position in the same slot.
/// </para>
/// <para>
/// A node that has no place cannot be duplicated and neither can the only child of a single-valued slot, which has
/// no "next" position. Both are refused with a reason rather than thrown, because the offer that produced this
/// action outlives the document it was derived from.
/// </para>
/// </remarks>
public sealed class DuplicateNodeAction : EditAction
{
    private readonly NodeId _node;

    /// <summary>Duplicates <paramref name="node"/> into the position after it.</summary>
    /// <param name="node">The node to copy, which stays where it is.</param>
    public DuplicateNodeAction(NodeId node) => _node = node;

    /// <summary>The node this action copies.</summary>
    public NodeId Node => _node;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.Document.Model.Project.FindNode(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this duplicate copies is no longer in the document.");
        }

        if (node.Parent is not { } parent || node.ParentSlot is not { } slot)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' is not inside anything, so it has no next sibling to be copied to.");
        }

        if (parent.Concept.FindChild(slot) is not { Cardinality: Cardinality.Many })
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{slot}' of concept '{parent.Concept.Key}' holds at most one child, so there is no next sibling.");
        }

        parent.Model.InsertCopy(node, parent, slot, node.IndexInParent + 1);

        return EditResult.Applied(selection);
    }

    public override string ToString() => $"duplicate {_node}";
}
