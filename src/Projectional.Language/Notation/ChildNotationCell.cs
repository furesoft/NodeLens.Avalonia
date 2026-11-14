namespace Projectional.Language;

/// <summary>
/// A child slot drawn as a block: every child contributes its own lines, stacked in order.
/// </summary>
public sealed class ChildNotationCell : BlockNotationCell
{
    public ChildNotationCell(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    public string Slot { get; }

    /// <summary>What an empty list looks like. Empty is never invisible, or the slot cannot be filled.</summary>
    public string EmptyText { get; init; } = "<empty>";

    public override string ToString() => $"children {Slot}";
}
