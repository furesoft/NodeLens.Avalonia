using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Points a reference at a node, or clears it, without needing a cell to stand in.</summary>
/// <remarks>
/// <para>
/// A repair knows what a reference should point at long before the caret is there: a finding about a dangling
/// reference is about the <em>model</em>, and the user may be looking at another line entirely. This action
/// therefore addresses the owner and the slot rather than a place on screen - the same reason
/// <see cref="SetNodePropertyAction"/> exists beside <see cref="SetPropertyAction"/>.
/// </para>
/// <para>
/// A target that is gone, or that the slot's concept does not accept, is a rejection with a reason rather than an
/// exception: a fix that outlived the document it was built for is an ordinary case, not a programming error.
/// Setting the target a reference already has, and clearing one that is already unset, are
/// <see cref="EditResult.Unchanged"/> - an undo step that changes nothing would make the history claim a change the
/// model never had.
/// </para>
/// <para>
/// The caret stays where it was, like <see cref="SetNodePropertyAction"/>: the user is reading one line and the
/// reference is usually drawn on another.
/// </para>
/// </remarks>
public sealed class SetReferenceAction : EditAction
{
    private readonly NodeId _owner;
    private readonly string _slot;
    private readonly NodeId? _target;

    /// <summary>Points the reference in <paramref name="slot"/> of node <paramref name="owner"/> at <paramref name="target"/>.</summary>
    /// <param name="owner">The node that owns the reference; it has to be part of the document being edited.</param>
    /// <param name="slot">The reference slot, which must belong to the node's concept.</param>
    /// <param name="target">The node to point at, or <see langword="null"/> to clear the reference.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public SetReferenceAction(NodeId owner, string slot, NodeId? target = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        _owner = owner;
        _slot = slot;
        _target = target is { IsEmpty: true } ? null : target;
    }

    /// <summary>The node whose reference is written.</summary>
    public NodeId Owner => _owner;

    /// <summary>The reference slot that is written.</summary>
    public string Slot => _slot;

    /// <summary>The node it points at, or <see langword="null"/> when the action clears the reference.</summary>
    public NodeId? Target => _target;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.NodeOf(_owner) is not { } node)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The document no longer holds the node '{_owner}' this writes to.");
        }

        // The declaration comes first: asking a node for the value of a slot that is not a reference throws, so
        // "what does it point at now" is a question that can only be asked once the slot is known to be one.
        if (node.Concept.FindReference(_slot) is null)
        {
            return EditResult.Rejected(
                context.Selection,
                $"'{node.Concept.Key}' declares no reference named '{_slot}'.");
        }

        if (_target is not { } targetId)
        {
            if (node.GetReference(_slot) is null)
            {
                return EditResult.Unchanged(context.Selection);
            }

            return context.ClearReference(node, _slot)
                ? EditResult.Applied(context.Selection)
                : EditResult.Rejected(context.Selection, $"The reference '{_slot}' could not be cleared.");
        }

        if (node.GetReference(_slot) is { } current && current.Target == targetId)
        {
            return EditResult.Unchanged(context.Selection);
        }

        // The target may live in another document of the project - a slot's scope offers those by name - so it is
        // looked up in the project rather than in the model the caret is in.
        if (node.Model.Project.FindNode(targetId) is not { } target)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The reference target '{targetId}' is no longer in this project.");
        }

        return context.SetReference(node, _slot, target)
            ? EditResult.Applied(context.Selection)
            : EditResult.Rejected(
                context.Selection,
                $"'{target.Concept.Key}' cannot stand in the reference '{_slot}'.");
    }

    public override string ToString() => _target is { } target
        ? $"point {_owner}.{_slot} at {target}"
        : $"clear {_owner}.{_slot}";
}