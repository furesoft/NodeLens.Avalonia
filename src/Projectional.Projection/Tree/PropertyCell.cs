using Projectional.Core;
using Projectional.Language;

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
public sealed class PropertyCell : TextCell
{
    public PropertyCell(
        CellAddress address,
        string slot,
        PropertyType type,
        string text,
        bool isSet,
        TextEmphasis emphasis = TextEmphasis.None,
        string emptyText = "")
        : base(address, text, emphasis)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(emptyText);

        Slot = slot;
        Type = type;
        IsSet = isSet;
        EmptyText = emptyText;
    }

    public string Slot { get; }

    /// <summary>What the text has to parse as, which is also what completion and validation read.</summary>
    public PropertyType Type { get; }

    /// <summary>Whether the slot holds a value, as opposed to being empty and waiting for input.</summary>
    public bool IsSet { get; }

    /// <summary>
    /// What the cell shows instead of the empty text, such as <c>&lt;&lt;operator&gt;&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The notation decides it, not the model, because it is a statement about what belongs in the slot rather
    /// than something the slot holds. Layout reserves its width, <see cref="CellText"/> answers with it and the
    /// placeholder is painted in the placeholder colour, while everything that edits the value keeps reading
    /// <see cref="TextCell.Text"/> - so an empty cell that shows a placeholder is still an empty cell to type into.
    /// </para>
    /// <para>
    /// Empty means "no placeholder", which is what every notation that does not ask for one gets.
    /// </para>
    /// </remarks>
    public string EmptyText { get; }

    internal override string Describe() => $"property {Slot}";
}
