namespace Projectional.Editing;

/// <summary>Which side of a node a new sibling is inserted on.</summary>
/// <remarks>
/// Named after the picture rather than after the list index, like <see cref="NodeMove"/>: "before" is the place the
/// node currently holds, and "after" is the one following it, so the two entries read the way the document looks.
/// </remarks>
public enum SiblingPlace
{
    /// <summary>In the place the named node holds, moving it and everything after it down one.</summary>
    Before,

    /// <summary>In the place after the named node.</summary>
    After,
}
