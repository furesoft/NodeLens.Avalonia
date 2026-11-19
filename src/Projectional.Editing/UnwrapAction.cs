using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Removes a node that wraps one child, leaving that child in the node's place.</summary>
/// <remarks>
/// <para>
/// The inverse of <see cref="WrapInAction"/>: <c>!!x</c> becomes <c>!x</c> when the outer negation is unwrapped,
/// and a repair that means "remove both" unwraps twice inside one <see cref="EditProgram"/>. The node it names is
/// deleted, so the action is <see cref="EditSafety.Destructive"/> at the offer that uses it.
/// </para>
/// <para>
/// It addresses the node rather than a cell, for the reason <see cref="SetReferenceAction"/> does: the finding is
/// about the model, and the caret may be on another line.
/// </para>
/// <para>
/// Everything the model refuses - a root, a slot holding more than one child, a child that may not stand where
/// the node stands, a node that holds more than the child it gives up - becomes a rejection with the model's own
/// sentence rather than an exception. The model stays the single authority on a legal placement; this action only
/// translates its answer into something a menu can show.
/// </para>
/// </remarks>
public sealed class UnwrapAction : EditAction
{
    private readonly NodeId _node;
    private readonly string _slot;

    /// <summary>Puts the only child of <paramref name="slot"/> where node <paramref name="node"/> stands.</summary>
    /// <param name="node">The node that gives up its place and is deleted.</param>
    /// <param name="slot">A child slot of its concept that holds exactly one child.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public UnwrapAction(NodeId node, string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        _node = node;
        _slot = slot;
    }

    /// <summary>The node that is unwrapped and deleted.</summary>
    public NodeId Node => _node;

    /// <summary>The slot whose only child takes its place.</summary>
    public string Slot => _slot;

    /// <inheritdoc/>
    /// <remarks>
    /// The caret stays where it was, like <see cref="WrapInAction"/>: what it was standing in is still on screen,
    /// one level further out. The one case it cannot keep is a caret inside the deleted node's own cells - there
    /// the rebuild resolves to the nearest surviving place, exactly as a delete does.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The document no longer holds the node '{_node}' this unwraps.");
        }

        if (node.Parent is null)
        {
            return EditResult.Rejected(
                context.Selection,
                $"Node '{node.Concept.Key}' is not inside anything, so nothing can take its place.");
        }

        if (node.Concept.FindChild(_slot) is null)
        {
            return EditResult.Rejected(
                context.Selection,
                $"'{node.Concept.Key}' declares no child slot named '{_slot}'.");
        }

        if (node.GetChildren(_slot).Count != 1)
        {
            return EditResult.Rejected(
                context.Selection,
                $"Slot '{_slot}' of '{node.Concept.Key}' holds {node.GetChildren(_slot).Count} children, so exactly one can take the node's place.");
        }

        try
        {
            return context.UnwrapChild(node, _slot)
                ? EditResult.Applied(context.Selection)
                : EditResult.Rejected(context.Selection, $"The node '{node.Concept.Key}' could not be unwrapped.");
        }
        catch (ModelInvariantException refused)
        {
            return EditResult.Rejected(context.Selection, refused.Message);
        }
    }

    public override string ToString() => $"unwrap {_node}.{_slot}";
}