using Projectional.Core;

namespace Projectional.Projection;

/// <summary>
/// The current value of a property slot, shown where the notation puts it.
/// </summary>
/// <remarks>
/// The cell carries the text and the property type instead of reaching back into the node, because a
/// cell is a snapshot: it is valid for exactly the model version it was built from, and reading the
/// node again during layout would let a stale tree show a value the layout did not measure. The type
/// travels with the text because editing a property means parsing what the user typed, and that parse
/// needs to know what is legal without asking the model again.
/// </remarks>
public sealed class PropertyCell : EditorCell
{
    public PropertyCell(CellAddress address, string slot, PropertyType type, string text, bool isSet)
        : base(address)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(text);

        Slot = slot;
        Type = type;
        Text = text;
        IsSet = isSet;
    }

    public string Slot { get; }

    /// <summary>What the text has to parse as, which is also what completion and validation read.</summary>
    public PropertyType Type { get; }

    /// <summary>The stored text, or the empty string while the slot has no value.</summary>
    public string Text { get; }

    /// <summary>Whether the slot holds a value, as opposed to being empty and waiting for input.</summary>
    public bool IsSet { get; }

    internal override string Describe() => $"property {Slot}";
}
