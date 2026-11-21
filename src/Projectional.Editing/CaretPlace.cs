using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One cell the caret can stop on, together with the text that its offset indexes into.
/// </summary>
/// <remarks>
/// <para>
/// Every leaf cell is a caret stop, because that is what <see cref="EditorCell.IsCaretTarget"/> says:
/// keywords and spaces are leaves too, and a caret that skipped them would not be able to step through
/// what the reader sees. The one exception is an annotation - a type hint the projection computed and drew
/// as an inlay - which is a leaf that answers no: it reads as text but is not part of the document, so the
/// caret steps over it rather than into it. What differs between stops is only what can be done at them - a
/// property can be typed into, an empty child slot can be filled, a constant can merely be passed through -
/// which is what <see cref="IsEditable"/> and <see cref="IsEmptySlot"/> report.
/// </para>
/// <para>
/// The text is copied alongside the cell rather than read through it because the cell kind decides how
/// to get it, and a navigation routine that patterned on cell kinds would be repeating what this type
/// already answers once.
/// </para>
/// </remarks>
public readonly record struct CaretPlace
{
    internal CaretPlace(EditorCell cell, string text, int order)
    {
        Cell = cell;
        Text = text;
        Order = order;
    }

    /// <summary>The cell the caret stands in.</summary>
    public EditorCell Cell { get; }

    /// <summary>The address of that cell.</summary>
    public CellAddress Address => Cell.Address;

    /// <summary>What the cell shows, which is what the offset counts characters of.</summary>
    public string Text { get; }

    /// <summary>
    /// How the text is decorated, and therefore which face it was measured in.
    /// </summary>
    /// <remarks>
    /// A caret offset has to be turned back into a horizontal position by measuring the text in front of
    /// it. Measuring with the regular face while the glyphs are drawn in a bold one would put the caret
    /// somewhere the letters are not, so the decoration travels with the stop.
    /// </remarks>
    public TextEmphasis Emphasis => Cell is TextCell text ? text.Emphasis : TextEmphasis.None;

    /// <summary>How many offsets the cell has, one of which is always its end.</summary>
    public int Length => Text.Length;

    /// <summary>Whether typing at this stop changes the model.</summary>
    public bool IsEditable => Cell is PropertyCell;

    /// <summary>Whether this stop is a child slot waiting to be filled.</summary>
    /// <remarks>
    /// A table's affordance row counts: it stands for the rows slot, which is empty, and it is the only
    /// place in an empty table where the first row can be created.
    /// </remarks>
    public bool IsEmptySlot =>
        Cell is ChildCell { IsEmpty: true } or CollectionCell { IsEmpty: true } or AddRowCell;

    /// <summary>Whether this stop is a place the user can put something, which is what Tab visits.</summary>
    public bool IsSlot => IsEditable || IsEmptySlot;

    /// <summary>Position of the cell in document order, used to compare a stop with a stale address.</summary>
    internal int Order { get; }

    public override string ToString() => $"{Address} '{Text}'";
}
