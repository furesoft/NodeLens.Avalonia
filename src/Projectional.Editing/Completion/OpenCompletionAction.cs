namespace Projectional.Editing;

/// <summary>
/// Ctrl+Space: offers the concepts that could fill the empty slot at the caret.
/// </summary>
/// <remarks>
/// <para>
/// Opening a list is not an edit, so the model and the undo history are untouched whether the caret is
/// in a fillable slot or not; the session reports <see cref="EditOutcome.Unchanged"/> in both cases and
/// the difference is that a list is open afterwards or it is not.
/// </para>
/// <para>
/// Nothing happens in three places on purpose: the caret is not in a slot at all, the slot already holds
/// a child (filling it again would replace a node, which this slice does not do), and the slot's declared
/// concept has no concrete concept below it (there is nothing to offer). Guessing in any of those cases
/// would put an offer on screen that accepting it could not honour.
/// </para>
/// </remarks>
public sealed class OpenCompletionAction : CompletionAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (CompletionSlot.At(context) is not { } slot
            || CompletionSource.Open(context, slot.Owner, slot.Slot) is not { } list)
        {
            context.Completion = null;

            return EditResult.Unchanged(context.Selection);
        }

        context.Completion = list;

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => "open completion";
}
