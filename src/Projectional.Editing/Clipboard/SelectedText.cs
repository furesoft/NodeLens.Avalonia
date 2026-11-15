using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The text under a selection, which is what Copy and Cut take away.
/// </summary>
/// <remarks>
/// One implementation rather than one per action, because "what does the selection say" has one honest
/// answer: the characters between its two ends inside the property cell the caret stands in, and nothing
/// when those two ends are not in the same cell. Both ends are clamped, because a selection may outlive a
/// shortening of the text it pointed at - a rebuild is not required to keep a range valid.
/// </remarks>
internal static class SelectedText
{
    /// <summary>The selected characters, or <see langword="null"/> when nothing inside one cell is selected.</summary>
    /// <param name="context">The apply being run.</param>
    public static string? Of(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (!selection.HasText || context.PlaceAt(selection.Caret)?.Cell is not PropertyCell cell)
        {
            return null;
        }

        var start = Math.Clamp(selection.Start, 0, cell.Text.Length);
        var end = Math.Clamp(selection.End, start, cell.Text.Length);

        return cell.Text[start..end];
    }
}
