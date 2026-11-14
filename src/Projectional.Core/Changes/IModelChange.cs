namespace Projectional.Core;

/// <summary>
/// One atomic edit of a model, described well enough for a subscriber to update only what the
/// edit touched.
/// </summary>
/// <remarks>
/// Changes are the backbone every other layer hangs off: projections invalidate what they show,
/// undo pushes the inverse, semantics dirties the constraints that depend on the node, and the host
/// application learns that it has unsaved work. They exist so that none of those layers has to
/// diff or re-read the tree.
/// </remarks>
public interface IModelChange
{
    /// <summary>The node the change is about: the node that appeared, disappeared or was written.</summary>
    NodeId Target { get; }

    /// <summary>
    /// The change that puts the model back the way it was before this one — what undo publishes so
    /// that subscribers learn about a restoration instead of having to guess at it.
    /// </summary>
    /// <remarks>
    /// Inverting is a pure record transformation, so it never looks at the tree. A move therefore
    /// inverts into a <see cref="NodeAdded"/> followed by a <see cref="NodeRemoved"/>, which is the
    /// move back.
    /// </remarks>
    IModelChange Invert();
}
