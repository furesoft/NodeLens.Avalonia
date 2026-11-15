namespace Projectional.Editing;

/// <summary>
/// Up or Down while a completion list is open: moves the highlight instead of the caret.
/// </summary>
/// <remarks>
/// The list is short and the caret is already where the node will go, so the arrows are worth more as a
/// selection inside the list than as a move. The caret does not move at all, which is why the edit is
/// <see cref="EditOutcome.Unchanged"/> even though something visibly happened.
/// </remarks>
public sealed class SelectCompletionAction(int delta) : CompletionAction
{
    /// <summary>How many entries to move, negative for up.</summary>
    public int Delta { get; } = delta;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Completion is { } list)
        {
            context.Completion = list.Move(Delta);
        }

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => $"select completion {Delta}";
}
