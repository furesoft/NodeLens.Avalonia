namespace Projectional.Language;

/// <summary>Every child of a list slot, in order, separated by a template.</summary>
/// <remarks>
/// The separator is a template rather than a string, and that is what makes a list on separate lines expressible:
/// a <see cref="LineBreakPart"/> separator indents every line it starts, while <c>", "</c> as a
/// <see cref="TextPart"/> joins in one line. A separator is emitted between children only - never before the first
/// or after the last - so a list of one child has no separator at all.
/// </remarks>
public sealed record ChildrenPart : TemplatePart
{
    /// <summary>Creates a part that expands every child of <paramref name="slot"/>.</summary>
    /// <param name="slot">The list slot to expand.</param>
    /// <param name="separator">What goes between two children; <see langword="null"/> means nothing.</param>
    /// <param name="blankLineBetweenGroups">
    /// Whether a boundary between two groups gets a blank line above it.
    /// </param>
    /// <param name="tightConcepts">
    /// The concepts whose neighbours share a group, so no blank line separates them. Every other boundary is a group
    /// boundary, so an empty set separates every child from the next.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public ChildrenPart(
        string slot,
        TextTemplate? separator = null,
        bool blankLineBetweenGroups = false,
        IReadOnlySet<string>? tightConcepts = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
        Separator = separator ?? TextTemplate.Empty;
        BlankLineBetweenGroups = blankLineBetweenGroups;
        TightConcepts = tightConcepts ?? ChildGroups.Empty;
    }

    /// <summary>The list slot to expand.</summary>
    public string Slot { get; }

    /// <summary>What goes between two children.</summary>
    public TextTemplate Separator { get; }

    /// <summary>
    /// Whether a boundary between two groups gets a blank line.
    /// </summary>
    /// <remarks>
    /// A group is a run of neighbouring children whose concepts are both in <see cref="TightConcepts"/>; every other
    /// boundary is a group boundary. The boundary gets one empty line, written as a bare newline before the
    /// separator: a separator indents the line it starts, so a blank line spelled as two separators would carry the
    /// indentation of the row after it and generate trailing whitespace. Nothing here is a fact about the model - the
    /// model holds no whitespace - so the template is what says where a group ends.
    /// <para>
    /// A group boundary is vertical space, so a language pairs this with a <em>stacked</em> child slot in the
    /// notation: a single-line list has nowhere to draw the gap, and a notation that set this flag without stacking
    /// would generate a newline the drawing does not show. The two are separate because generation and projection are
    /// separate: the generator writes text, the notation places cells, and both read the same rule.
    /// </para>
    /// </remarks>
    public bool BlankLineBetweenGroups { get; }

    /// <summary>
    /// The concepts whose neighbours share a group, so that no blank line separates them.
    /// </summary>
    /// <remarks>
    /// The .NET convention is "a blank line between members, except inside a block of fields", which is one set with
    /// one entry in it. An empty set means every child is its own group, so every boundary gets a blank line - the
    /// convention's own default, and the reason the set is the exception rather than the rule.
    /// </remarks>
    public IReadOnlySet<string> TightConcepts { get; }

    public override string ToString() => $"children {Slot}";
}
