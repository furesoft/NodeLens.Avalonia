using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Selects the whole text of the property the caret is in.
/// </summary>
/// <remarks>
/// <para>
/// "Everything" in a projectional editor cannot mean the document, because a selection is a range inside
/// one cell - the text actions all work on one property's text, and a range spanning two cells would have
/// nothing to act on. So Ctrl+A selects the property the caret stands in, with the caret at its end,
/// which is what makes the next keystroke replace that property's whole value. Pressing it again is a
/// no-op rather than a third state, because a selection has nowhere to widen to beyond one cell.
/// </para>
/// <para>
/// A caret in a keyword, a space or an empty slot changes nothing. Those cells have no editable text, and
/// selecting the notation's own word would produce a selection no later action can use.
/// </para>
/// </remarks>
public sealed class SelectAllAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="EditOutcome.Unchanged"/>, because the model is untouched and only the selection moves -
    /// the same outcome <see cref="MoveCaretAction"/> reports for the same reason.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PlaceAt(context.Selection.Caret) is not { } place ||
            place.Cell is not PropertyCell cell ||
            cell.Text.Length == 0)
        {
            return EditResult.Unchanged(context.Selection);
        }

        return EditResult.Unchanged(
            new Selection(CaretPosition.At(place, 0), CaretPosition.At(place, cell.Text.Length)));
    }

    public override string ToString() => "select all";
}
