namespace Projectional.Language;

/// <summary>A property slot shown as editable text.</summary>
/// <remarks>
/// The cell holds the slot name, not the value: the value belongs to the node, and the same notation
/// object is shared by every node of the concept.
/// </remarks>
public sealed class PropertyNotationCell : InlineNotationCell
{
    public PropertyNotationCell(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    public string Slot { get; }

    public override string ToString() => $"property {Slot}";
}
