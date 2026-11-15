namespace Projectional.Editing;

/// <summary>
/// A typed character while a completion list is open: filters the proposals instead of editing.
/// </summary>
/// <remarks>
/// <para>
/// The caret of an open list is in an <em>empty</em> slot, so there is no text to insert into and the
/// character can only be a filter. That is why typing while the list is open is not an edit at all, and
/// why the characters live on the list rather than in the model: dismissing the list must leave the
/// document exactly as it was.
/// </para>
/// <para>
/// A character that matches nothing does not close the list - it empties it, and the next backspace
/// brings the proposals back. Closing on a miss would mean the user pays for a typo by reopening.
/// </para>
/// </remarks>
public sealed class NarrowCompletionAction(string text) : CompletionAction
{
    /// <summary>The characters that were typed.</summary>
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Completion is { } list)
        {
            context.Completion = list.Narrow(Text);
        }

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => $"narrow completion by '{Text}'";
}
