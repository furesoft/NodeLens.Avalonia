namespace Projectional.Editing;

/// <summary>
/// Puts the selected text on the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// Only a selection is copied. With nothing selected there is no entity to copy either: a cell is a piece
/// of notation, not a document object, and "copy the word under the caret because the user pressed Ctrl+C"
/// is a behaviour no editor of text has. Copying a whole node is a different feature - it produces a
/// structural fragment that can be pasted into another slot - and it is planned for M3 rather than being
/// guessed at here.
/// </para>
/// <para>
/// The text goes to whatever <see cref="EditContext.Clipboard"/> the session was built with, which is the
/// operating-system clipboard when the host supplied an adapter for it. The action therefore neither knows
/// nor cares whether the text leaves the process.
/// </para>
/// </remarks>
public sealed class CopyAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="EditOutcome.Unchanged"/> in every case: writing to the clipboard is not a change to the
    /// document, so the host has no tree to rebuild and no undo step is produced for a copy.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (SelectedText.Of(context) is { } text)
        {
            context.Clipboard.Text = text;
        }

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => "copy";
}
