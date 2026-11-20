using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Puts a copy of a node into a stated place: a slot of another parent, at an index.</summary>
/// <remarks>
/// <para>
/// <see cref="DuplicateNodeAction"/> copies a node next to itself, which is what the user asked for when they said
/// "duplicate". A refactoring asks a different question - "put what that node holds here instead" - and the place it
/// names is a parent, a slot and an index rather than the source's own neighbourhood.
/// </para>
/// <para>
/// The copy is a new subtree with new identities (<see cref="Model.InsertCopy"/>), which is the difference between this
/// and <see cref="MoveNodeToAction"/>: the source stays where it is, so a use can be given the definition's content
/// without taking it away from every other use. What the copy shares with a move is the answer to a stale place: a
/// reason rather than a throw, because a program computed it earlier.
/// </para>
/// </remarks>
public sealed class InsertCopyAction : EditAction
{
    private readonly NodeId _source;
    private readonly NodeId _parent;
    private readonly string _slot;
    private readonly int _index;

    /// <summary>Copies <paramref name="source"/> into <paramref name="slot"/> of <paramref name="parent"/>, at <paramref name="index"/>.</summary>
    /// <param name="source">The node to copy, which stays where it is.</param>
    /// <param name="parent">The node that should own the copy.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/> that receives it.</param>
    /// <param name="index">Its place among that slot's children, clamped to the end of the list.</param>
    /// <exception cref="ArgumentException"><paramref name="source"/> or <paramref name="parent"/> is empty, or <paramref name="slot"/> is not.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public InsertCopyAction(NodeId source, NodeId parent, string slot, int index)
    {
        if (source.IsEmpty)
        {
            throw new ArgumentException("A copy needs a node to copy.", nameof(source));
        }

        if (parent.IsEmpty)
        {
            throw new ArgumentException("A copy needs a place to go.", nameof(parent));
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        _source = source;
        _parent = parent;
        _slot = slot;
        _index = index;
    }

    /// <summary>The node that is copied.</summary>
    public NodeId Source => _source;

    /// <summary>The node that receives the copy.</summary>
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

        if (context.NodeOf(_source) is not { } source)
        {
            return EditResult.Rejected(selection, "The node this entry copies is no longer in the document.");
        }

        if (context.NodeOf(_parent) is not { } parent)
        {
            return EditResult.Rejected(selection, "The node this entry copies into is no longer in the document.");
        }

        if (parent.Concept.FindChild(_slot) is not { } place)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{parent.Concept.Key}' declares no child slot '{_slot}', so nothing can be copied there.");
        }

        if (!source.Concept.IsKindOf(place.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{source.Concept.Key}' cannot stand in slot '{_slot}', which holds '{place.ChildConceptKey}'.");
        }

        if (place.Cardinality == Cardinality.One && parent.GetChildren(_slot).Count > 0)
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{_slot}' of '{parent.Concept.Key}' holds at most one child, and it is filled.");
        }

        parent.Model.InsertCopy(source, parent, _slot, _index);

        return EditResult.Applied(selection);
    }

    /// <inheritdoc/>
    public override string ToString() => $"copy {_source} into {_parent}.{_slot} at {_index}";
}
