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
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public ChildrenPart(string slot, TextTemplate? separator = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
        Separator = separator ?? TextTemplate.Empty;
    }

    /// <summary>The list slot to expand.</summary>
    public string Slot { get; }

    /// <summary>What goes between two children.</summary>
    public TextTemplate Separator { get; }

    public override string ToString() => $"children {Slot}";
}
