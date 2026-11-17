using Projectional.Core;

namespace Projectional.Editing;

/// <summary>
/// Moves the node the caret is in one place up or down among the children of its slot.
/// </summary>
/// <remarks>
/// <para>
/// The order of a collection is part of the model, not of the picture, so reordering is an edit like
/// typing: it goes through the model's own mutators, lands in the journal as one undo step, and leaves
/// the tree to be rebuilt afterwards. The node keeps its identity, which is why the caret stays on it -
/// an address names a node, not a position on screen, so a caret that was in the moved node is still in
/// it after the move.
/// </para>
/// <para>
/// A node with no sibling to trade places with does nothing rather than wrapping around, for the same
/// reason <see cref="TabAction"/> stops at the ends: a shortcut that jumps from the last node to the
/// first is indistinguishable from one that fired at the wrong moment.
/// </para>
/// <para>
/// The move acts on the node the caret is in, so it also works from a slot of that node - moving the
/// rule whose <c>name</c> is being edited is what the user means. A caret outside any node, which is the
/// document root, has nothing to move, and neither has a caret in a node that is its parent's only
/// child: climbing to the nearest ancestor that could move is a separate decision, because which
/// ancestor the user meant is a guess the caret does not make.
/// </para>
/// </remarks>
public sealed class MoveNodeAction : EditAction
{
    /// <summary>Creates the action for one direction.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="move"/> is not a defined move.</exception>
    public MoveNodeAction(NodeMove move)
    {
        if (!Enum.IsDefined(move))
        {
            throw new ArgumentOutOfRangeException(nameof(move), move, "Unknown node move.");
        }

        Move = move;
    }

    /// <summary>Which way to move.</summary>
    public NodeMove Move { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="EditOutcome.Unchanged"/> when there is nothing to move - no parent, or already
    /// the first or last child - so the host can use that answer the way it uses a Tab that did nothing.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.NodeOf(selection.Caret.Address) is not { } node
            || node.Parent is not { } parent
            || node.ParentSlot is not { } slot)
        {
            return EditResult.Unchanged(selection);
        }

        var target = Move switch
        {
            NodeMove.ToFront => 0,
            NodeMove.ToBack => parent.GetChildren(slot).Count - 1,
            _ => node.IndexInParent + (Move == NodeMove.Up ? -1 : 1),
        };

        if (target < 0 || target >= parent.GetChildren(slot).Count || target == node.IndexInParent)
        {
            return EditResult.Unchanged(selection);
        }

        context.MoveChild(parent, slot, node, target);

        return EditResult.Applied(selection);
    }

    public override string ToString() => Move switch
    {
        NodeMove.ToFront => "move node to the front",
        NodeMove.ToBack => "move node to the back",
        _ => Move == NodeMove.Up ? "move node up" : "move node down",
    };
}
