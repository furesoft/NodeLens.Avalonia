namespace Projectional.Editing;

/// <summary>
/// Copies the selected text and then deletes it.
/// </summary>
/// <remarks>
/// <para>
/// Cut is copy followed by delete, and it delegates both. Writing the text to the clipboard again here
/// would be a second answer to "what does the selection say", and deleting by hand would be a second
/// answer to "what does deleting a range mean" - including the type check that decides whether the shortened
/// value is still valid. So this action only orders the two.
/// </para>
/// <para>
/// When nothing is selected it does exactly what <see cref="DeleteTextAction"/> does in that situation:
/// nothing, because Backspace-style deletion of the character next to the caret is not what Cut means and
/// would silently destroy a character the user had not chosen.
/// </para>
/// <para>
/// The refused case is the interesting one. A property whose value would become invalid cannot be cut -
/// the copy has already happened by then, so the clipboard holds text the document still contains. That is
/// the honest outcome: the user can paste it somewhere it fits.
/// </para>
/// </remarks>
public sealed class CutAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// The result of the deletion, so the caret lands where deleting the range leaves it. A refused
    /// deletion is passed through as the rejection it is rather than being reported as a successful cut.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (SelectedText.Of(context) is not { } text)
        {
            return EditResult.Unchanged(context.Selection);
        }

        context.Clipboard.Text = text;

        return new DeleteTextAction(DeleteDirection.Forward).Apply(context);
    }

    /// <summary>Never coalesces, so a cut is one undo step of its own.</summary>
    /// <remarks>
    /// Deleting a selection reports no key either, but saying so here makes the guarantee independent of
    /// what the delegate decides - a future delete that coalesced ranges would still not merge a cut into
    /// the typing around it.
    /// </remarks>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return null;
    }

    public override string ToString() => "cut";
}
