namespace Projectional.Editing;

/// <summary>
/// Backspace while a completion list is open: removes the last filter character and offers more again.
/// </summary>
/// <remarks>
/// With no filter character left there is nothing to widen, so the key falls through to the session's
/// ordinary backspace - which is what closes the list, because deleting text is not a completion
/// gesture. That fall-through is decided where the keys are routed, not here: this action always means
/// "the filter got one character shorter".
/// </remarks>
public sealed class WidenCompletionAction : CompletionAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Completion is { } list)
        {
            context.Completion = list.Widen();
        }

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => "widen completion";
}
