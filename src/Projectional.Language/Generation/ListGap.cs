namespace Projectional.Language;

/// <summary>Where a list leaves an empty line, and it is always an empty line rather than a cell.</summary>
/// <remarks>
/// A gap is vertical space, so only a stacked list can draw one. The three positions were three separate flags before
/// the third arrived, which made the constructor of <see cref="ChildrenPart"/> longer than the thing it describes; one
/// value keeps "where does this list breathe" a single question. The positions are independent - a list can open with a
/// gap and put none between its children - so they are flags rather than a choice.
/// <para>
/// <see cref="BetweenGroups"/> is the only position that depends on what is <em>in</em> the list: it asks
/// <see cref="ChildGroups"/> where one run of a declared kind ends. The other two describe where the list itself sits,
/// which is why they are not entries in a tight set.
/// </para>
/// </remarks>
[Flags]
public enum ListGap
{
    /// <summary>No gap: every child follows the one before it directly.</summary>
    None = 0,

    /// <summary>An empty line above every child that starts a new group.</summary>
    BetweenGroups = 1,

    /// <summary>An empty line above the first child, for a list the notation puts under a header of its own.</summary>
    BeforeFirst = 2,

    /// <summary>An empty line below the last child, for a list that continues into something else.</summary>
    AfterLast = 4,
}
