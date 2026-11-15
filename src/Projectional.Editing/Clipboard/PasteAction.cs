namespace Projectional.Editing;

/// <summary>
/// Types whatever the clipboard holds into the property the caret is in.
/// </summary>
/// <remarks>
/// <para>
/// Pasting is typing, and it delegates rather than reimplementing it: the same replacement of the
/// selection or of the empty range at the caret, the same validity check, and the same refusal when the
/// result would not be a value of the property's type. A number cell therefore refuses pasted prose exactly
/// the way it refuses typed prose, which is the behaviour a projectional editor wants - the model is the
/// authority on what its values are, not the clipboard.
/// </para>
/// <para>
/// An empty clipboard is not an error, it is nothing to do. So is a clipboard holding an empty string, so
/// pressing Ctrl+V twice with one character copied does not look like the second press failed.
/// </para>
/// <para>
/// A paste never merges into the typing around it, even though it is carried out by
/// <see cref="TypeTextAction"/>. Two keystrokes are one burst of typing; a paste is a separate act that the
/// user will want to undo on its own.
/// </para>
/// </remarks>
public sealed class PasteAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// The result of typing the clipboard's text, which is <see cref="EditOutcome.Rejected"/> when the text
    /// does not fit the property and the caret is left where it was.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Clipboard.Text is not { Length: > 0 } text)
        {
            return EditResult.Unchanged(context.Selection);
        }

        return new TypeTextAction(text).Apply(context);
    }

    /// <summary>Never coalesces, so a paste is one undo step of its own.</summary>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return null;
    }

    public override string ToString() => "paste";
}
