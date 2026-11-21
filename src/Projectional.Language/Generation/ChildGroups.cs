namespace Projectional.Language;

/// <summary>Where one run of neighbouring children ends and the next begins.</summary>
/// <remarks>
/// A list can draw its children as groups, with a blank line between groups. A group is a run of neighbours whose
/// concepts are both in the tight set: the .NET convention's "a blank line between members, except inside a block of
/// fields" is one set with one entry in it.
/// <para>
/// The rule lives here rather than in the two places that read it because the drawing and the generated text have to
/// put the blank line in the same place. The notation cell and the generator's list part each carry the same set, and
/// both ask this type, so a disagreement is impossible rather than merely unlikely.
/// </para>
/// </remarks>
public static class ChildGroups
{
    /// <summary>The set with nothing in it, which makes every neighbour a group boundary of its own.</summary>
    public static IReadOnlySet<string> Empty { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Whether a blank line belongs between two neighbouring children.</summary>
    /// <param name="previous">The concept key of the child before, or <see langword="null"/> for the first one.</param>
    /// <param name="current">The concept key of the child being drawn.</param>
    /// <param name="tight">The concepts whose neighbours share a group.</param>
    /// <remarks>
    /// The first child never starts a boundary: there is nothing above it to separate it from. An empty tight set
    /// makes every boundary a group boundary, which is the convention's own default.
    /// </remarks>
    public static bool IsBoundary(string? previous, string current, IReadOnlySet<string> tight) =>
        previous is null || !tight.Contains(previous) || !tight.Contains(current);
}
