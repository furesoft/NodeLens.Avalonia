namespace Projectional.Projection;

/// <summary>
/// What a cell shows, which is the one question a renderer, a hit test and a caret all ask of a cell
/// before they can do anything with it.
/// </summary>
/// <remarks>
/// <para>
/// Cells of every kind can be asked, and the kinds that carry no text answer with the empty string
/// rather than with null: a container draws nothing but is still a cell, and a caller that had to test
/// for null as well would be repeating knowledge that belongs to the cell.
/// </para>
/// <para>
/// An empty slot shows its placeholder instead of nothing. Layout has already reserved room for it and
/// the caret can stand in it, so a language that drew nothing at all would leave the reader unable to
/// see that the slot is there at all.
/// </para>
/// </remarks>
public static class CellText
{
    /// <summary>The text <paramref name="cell"/> draws, empty when it draws nothing.</summary>
    public static string Of(EditorCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        return cell switch
        {
            TextCell text => text.Text,
            ChildCell { IsEmpty: true } empty => empty.EmptyText,
            CollectionCell { IsEmpty: true } empty => empty.EmptyText,
            _ => string.Empty,
        };
    }

    /// <summary>Whether <paramref name="cell"/> draws anything at all.</summary>
    public static bool IsVisible(EditorCell cell) => Of(cell).Length > 0;
}
