namespace Projectional.Language;

/// <summary>A property slot shown as editable text.</summary>
/// <remarks>
/// The cell holds the slot name, not the value: the value belongs to the node, and the same notation
/// object is shared by every node of the concept.
/// </remarks>
public sealed class PropertyNotationCell : InlineNotationCell
{
    public PropertyNotationCell(string slot, TextEmphasis emphasis = TextEmphasis.None)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
        Emphasis = emphasis;
    }

    public string Slot { get; }

    /// <summary>How the value is decorated wherever this notation shows it.</summary>
    public TextEmphasis Emphasis { get; }

    public override string ToString() => $"property {Slot}";
}
