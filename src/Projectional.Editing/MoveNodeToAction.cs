using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Moves a node to a stated place: a slot of another parent, at an index.</summary>
/// <remarks>
/// <para>
/// <see cref="MoveNodeAction"/> answers the picture - up, down, to the front - and every one of those stays inside the
/// slot the node already stands in. A refactoring cannot: "extract this" takes a subtree out of where it is and puts it
/// somewhere the language declared, and the place it names is a parent, a slot and an index rather than a direction.
/// This is that edit, addressed by id like the other structural entries, so a program can state a destination it
/// computed.
/// </para>
/// <para>
/// The node keeps its identity: the model detaches it and inserts it again inside one mutation, so every reference that
/// points into its subtree still resolves and every caret address that names one of its cells still lands. The journal
/// sees a removal and an addition in one transaction, which is why undo puts the node back in the slot it came from
/// rather than rebuilding a copy somewhere.
/// </para>
/// <para>
/// Everything a destination can refuse is answered as a reason rather than thrown, because a program that computed a
/// place from a declaration can be stale by the time it runs - the node it meant may have been deleted, or the slot may
/// have been filled by an edit earlier in the same program. A refactoring that threw would abandon the rest of the
/// program with the model half-changed.
/// </para>
/// </remarks>
public sealed class MoveNodeToAction : EditAction
{
    private readonly NodeId _node;
    private readonly NodeId _parent;
    private readonly string _slot;
    private readonly int _index;

    /// <summary>Moves <paramref name="node"/> into <paramref name="slot"/> of <paramref name="parent"/>, at <paramref name="index"/>.</summary>
    /// <param name="node">The node to move.</param>
    /// <param name="parent">The node that should own it.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/> that receives it.</param>
    /// <param name="index">Its place among that slot's children, clamped to the end of the list.</param>
    /// <exception cref="ArgumentException"><paramref name="node"/> or <paramref name="parent"/> is empty, or <paramref name="slot"/> is not.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public MoveNodeToAction(NodeId node, NodeId parent, string slot, int index)
    {
        if (node.IsEmpty)
        {
            throw new ArgumentException("A move needs a node.", nameof(node));
        }

        if (parent.IsEmpty)
        {
            throw new ArgumentException("A move needs a place to put the node.", nameof(parent));
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        _node = node;
        _parent = parent;
        _slot = slot;
        _index = index;
    }

    /// <summary>The node that moves.</summary>
    public NodeId Node => _node;

    /// <summary>The node that receives it.</summary>
    public NodeId Parent => _parent;

    /// <summary>The child slot of <see cref="Parent"/> it lands in.</summary>
    public string Slot => _slot;

    /// <summary>Its place among that slot's children.</summary>
    public int Index => _index;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this entry moves is no longer in the document.");
        }

        if (context.NodeOf(_parent) is not { } parent)
        {
            return EditResult.Rejected(selection, "The node this entry moves into is no longer in the document.");
        }

        if (parent.Concept.FindChild(_slot) is not { } place)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{parent.Concept.Key}' declares no child slot '{_slot}', so nothing can be moved there.");
        }

        if (ReferenceEquals(node, parent) || node.IsAncestorOf(parent))
        {
            return EditResult.Rejected(selection, "A node cannot be moved into itself.");
        }

        if (!node.Concept.IsKindOf(place.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' cannot stand in slot '{_slot}', which holds '{place.ChildConceptKey}'.");
        }

        var siblings = parent.GetChildren(_slot);

        if (place.Cardinality == Cardinality.One && siblings.Count > 0 && !ReferenceEquals(siblings[0], node))
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{_slot}' of '{parent.Concept.Key}' holds at most one child, and it is filled.");
        }

        if (ReferenceEquals(node.Parent, parent)
            && node.ParentSlot == _slot
            && node.IndexInParent == Math.Min(_index, siblings.Count - 1))
        {
            return EditResult.Unchanged(selection);
        }

        context.MoveChild(parent, _slot, node, _index);

        return EditResult.Applied(selection);
    }

    /// <inheritdoc/>
    public override string ToString() => $"move {_node} into {_parent}.{_slot} at {_index}";
}
