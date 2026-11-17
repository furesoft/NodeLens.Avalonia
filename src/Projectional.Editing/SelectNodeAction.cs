using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Selects a named node and everything drawn inside it.</summary>
/// <remarks>
/// <para>
/// The ladder <see cref="SelectAllAction"/> climbs is a sequence of presses, so it can only offer the units the caret
/// happens to be inside, one per press. This action is the same rung asked for by name: "select the parent" is the
/// widening step a user wants when the caret is already deep, and it must be reachable in one gesture - which is what
/// the catalogue's `INT008` needs.
/// </para>
/// <para>
/// It is a navigation and answers <see cref="EditResult.Unchanged"/>, exactly like <see cref="SelectSlotAction"/> and
/// <see cref="MoveCaretAction"/>: the model is untouched, so no undo step is created and nothing is saved. A node that
/// is no longer in the document is <see cref="EditResult.Rejected"/> with a reason, because an offer outlives the
/// document it was derived from.
/// </para>
/// <para>
/// A node whose notation draws no cells at all has no range to select, so the caret is left where it is rather than
/// collapsing the selection onto an address nothing covers.
/// </para>
/// </remarks>
public sealed class SelectNodeAction : EditAction
{
    private readonly NodeId _node;

    /// <summary>Selects <paramref name="node"/> and everything inside it.</summary>
    /// <param name="node">The node to select.</param>
    /// <exception cref="ArgumentException"><paramref name="node"/> is empty.</exception>
    public SelectNodeAction(NodeId node)
    {
        if (node.IsEmpty)
        {
            throw new ArgumentException("A selection needs a node.", nameof(node));
        }

        _node = node;
    }

    /// <summary>The node this action selects.</summary>
    public NodeId Node => _node;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this entry selects is no longer in the document.");
        }

        return SelectionRanges.OfNode(context, node) is { } range
            ? EditResult.Unchanged(range)
            : EditResult.Unchanged(selection);
    }

    public override string ToString() => $"select {_node}";
}
