namespace Projectional.Editing;

/// <summary>
/// Ctrl+Space: offers what could fill the slot at the caret - the concepts for an empty slot, the legal
/// values for a property whose type names them.
/// </summary>
/// <remarks>
/// <para>
/// Opening a list is not an edit, so the model and the undo history are untouched whether the caret is
/// in a completable place or not; the session reports <see cref="EditOutcome.Unchanged"/> in both cases
/// and the difference is that a list is open afterwards or it is not.
/// </para>
/// <para>
/// Nothing happens in four places on purpose: the caret is not in a slot at all, the slot already holds
/// a child (filling it again would replace a node, which this slice does not do), the slot's declared
/// concept has no concrete concept below it, and a property's type names no values (a string or a number
/// has nothing to offer). Guessing in any of those cases would put an offer on screen that accepting it
/// could not honour. Note that a <em>filled</em> property is the one filled slot that is not refused: a
/// value is replaced rather than added, which is what <see cref="AcceptCompletionAction"/> writes.
/// </para>
/// </remarks>
public sealed class OpenCompletionAction : CompletionAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (CompletionSlot.At(context) is not { } slot
            || CompletionSource.Open(context, slot) is not { } list)
        {
            context.Completion = null;

            return EditResult.Unchanged(context.Selection);
        }

        context.Completion = list;

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => "open completion";
}
