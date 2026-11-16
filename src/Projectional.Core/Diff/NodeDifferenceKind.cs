namespace Projectional.Core;

/// <summary>How one node differs between two models.</summary>
/// <remarks>
/// Five kinds, and no "unchanged": a diff that listed what did not change would be a diff of the whole
/// document. A node may appear more than once - it can be moved and have two properties rewritten - which is
/// what lets a renderer mark each change separately instead of summarising them.
/// </remarks>
public enum NodeDifferenceKind
{
    /// <summary>The node exists only in the later model, and so does everything below it.</summary>
    Added,

    /// <summary>The node exists only in the earlier model, with everything that was below it.</summary>
    Removed,

    /// <summary>The node exists in both and stands somewhere else: another parent, slot or index.</summary>
    Moved,

    /// <summary>The same property holds another value.</summary>
    PropertyChanged,

    /// <summary>The same reference points at something else, or its fallback text does.</summary>
    ReferenceChanged,
}
