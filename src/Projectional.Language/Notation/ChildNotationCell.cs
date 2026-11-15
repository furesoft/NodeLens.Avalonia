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

    public override string ToString() => $"children {Slot}";
}
