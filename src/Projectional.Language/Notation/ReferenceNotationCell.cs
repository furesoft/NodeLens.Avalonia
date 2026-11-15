namespace Projectional.Language;

/// <summary>A reference slot shown as the name of its target.</summary>
public sealed class ReferenceNotationCell : InlineNotationCell
{
    public ReferenceNotationCell(string slot, TextEmphasis emphasis = TextEmphasis.None)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
        Emphasis = emphasis;
    }

    public string Slot { get; }

    /// <summary>What an unset reference looks like, so the slot stays visible and reachable.</summary>
    public string UnsetText { get; init; } = "<none>";

    /// <summary>How the target's name is decorated wherever this notation shows it.</summary>
    public TextEmphasis Emphasis { get; }

    public override string ToString() => $"reference {Slot}";
}
