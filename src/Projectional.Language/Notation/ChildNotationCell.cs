namespace Projectional.Language;

/// <summary>
/// A child slot drawn as a block: every child contributes its own lines, stacked in order.
/// </summary>
public sealed class ChildNotationCell : BlockNotationCell
{
    private int _spacing = 1;

    public ChildNotationCell(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    public string Slot { get; }

    /// <summary>What an empty list looks like. Empty is never invisible, or the slot cannot be filled.</summary>
    public string EmptyText { get; init; } = "<empty>";

    /// <summary>
    /// How many spaces stand between two neighbouring children, and therefore also between the lines
    /// they end on.
    /// </summary>
    /// <remarks>
    /// A child slot has no separator to hang a blank on, so the spacing is the whole gap. The default of
    /// one keeps two children from touching when a notation puts them on the same line.
    /// </remarks>
    public int Spacing
    {
        get => _spacing;
        init => _spacing = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The spacing of a child slot cannot be negative.");
    }

    /// <summary>
    /// Whether every child is drawn on a line of its own instead of beside its neighbours.
    /// </summary>
    /// <remarks>
    /// The child slot of a block language usually wants one child per line and the child slot of an
    /// expression wants them side by side, so the direction is a property of the notation rather than a
    /// separate cell kind: both draw the same cell holding the same children. <see cref="Spacing"/> is
    /// the gap <em>between</em> children in a line and does not apply here - stacked children already
    /// start on a new line.
    /// </remarks>
    public bool Stacked { get; init; }

    /// <summary>Whether a boundary between two groups gets a blank line.</summary>
    /// <remarks>
    /// Only a stacked child cell can draw one: a group boundary is vertical space, so on a single line there is
    /// nowhere to put it. The rule is the same one the generator's <c>ChildrenPart</c> applies, so the drawing and
    /// the generated text put the blank line in the same place.
    /// </remarks>
    public bool BlankLineBetweenGroups { get; init; }

    /// <summary>
    /// The concepts whose neighbours share a group, so that no blank line separates them.
    /// </summary>
    /// <remarks>
    /// The same set the generator's <c>ChildrenPart</c> carries, read by the same <see cref="ChildGroups"/> rule, so
    /// the drawing and the generated text cannot disagree about where a group ends.
    /// </remarks>
    public IReadOnlySet<string> TightConcepts { get; init; } = ChildGroups.Empty;

    public override string ToString() => Stacked ? $"children {Slot} (stacked)" : $"children {Slot}";
}
