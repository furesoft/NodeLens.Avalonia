namespace Projectional.Editing;

/// <summary>What a preview mark says happened to one cell.</summary>
/// <remarks>
/// Three marks, and no "unchanged": a ghost marks what the edit did, and everything else is drawn plainly (dimmed
/// by one step) so that the marks read first. The kinds are meanings rather than colours - which tint an added
/// cell gets is the palette's business, exactly like <c>TextStyleKind</c>.
/// </remarks>
public enum CellChangeKind
{
    /// <summary>The cell's node does not exist in the live document.</summary>
    Added,

    /// <summary>The node exists in both, and a property, a reference or its place differs.</summary>
    Changed,

    /// <summary>The node existed at this place in the live document and is gone.</summary>
    Removed,
}
