namespace Projectional.Language;

/// <summary>A reference slot shown as the name of its target.</summary>
public sealed class ReferenceNotationCell : InlineNotationCell
{
    public ReferenceNotationCell(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    public string Slot { get; }

    /// <summary>What an unset reference looks like, so the slot stays visible and reachable.</summary>
    public string UnsetText { get; init; } = "<none>";

    public override string ToString() => $"reference {Slot}";
}
