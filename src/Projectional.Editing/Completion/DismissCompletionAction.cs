namespace Projectional.Editing;

/// <summary>
/// Escape: closes the completion list and leaves everything else alone.
/// </summary>
/// <remarks>
/// The caret stays where it was and the model is untouched, so dismissing is always safe - a user who
/// does not like the proposals presses Escape and is exactly where they started, with the same undo
/// history they had.
/// </remarks>
public sealed class DismissCompletionAction : CompletionAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Completion = null;

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => "dismiss completion";
}
