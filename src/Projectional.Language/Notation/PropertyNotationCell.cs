namespace Projectional.Language;

/// <summary>A property slot shown as editable text.</summary>
/// <remarks>
/// The cell holds the slot name, not the value: the value belongs to the node, and the same notation
/// object is shared by every node of the concept.
/// </remarks>
public sealed class PropertyNotationCell : InlineNotationCell
{
    public PropertyNotationCell(
        string slot,
        TextEmphasis emphasis = TextEmphasis.None,
        string emptyText = "")
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(emptyText);

        Slot = slot;
        Emphasis = emphasis;
        EmptyText = emptyText;
    }

    public string Slot { get; }

    /// <summary>How the value is decorated wherever this notation shows it.</summary>
    public TextEmphasis Emphasis { get; }

    /// <summary>
    /// What the cell shows while the slot holds no value, such as <c>&lt;&lt;operator&gt;&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A property is the only place in a projectional document where "nothing entered yet" is a visible state:
    /// the cell is drawn whether or not it holds a value, and without a placeholder it is a blank of one space
    /// that tells the reader nothing. A language whose empty value is a question - which operator, which key -
    /// says so here, and the reader sees what is expected before typing it.
    /// </para>
    /// <para>
    /// It is display only: not a value, never stored, never typed over, and the caret keeps standing in an empty
    /// text. Empty means "show nothing", which is the default, so a language that does not need it draws exactly
    /// as it did before.
    /// </para>
    /// </remarks>
    public string EmptyText { get; }

    public override string ToString() => $"property {Slot}";
}
