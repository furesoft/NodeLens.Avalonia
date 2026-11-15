namespace Projectional.Editing;

/// <summary>
/// Selects the whole text of the editable cell the caret stands in.
/// </summary>
/// <remarks>
/// <para>
/// This is the first rung of <see cref="SelectAllAction"/> on its own, for the gesture that only ever
/// means one thing: a third click on a value. Clicking three times on <c>Approved</c> says "this whole
/// cell", because the next keystroke is meant to replace it, and asking for the node or the document
/// instead would answer a question the user did not ask. <see cref="SelectWordAction"/> is the click
/// before it, which takes one word out of the same cell.
/// </para>
/// <para>
/// A cell that is not editable has nothing to select, so the action leaves the selection exactly where
/// the press put it rather than widening to something else. A click on a keyword, on a placeholder or on
/// a reference therefore behaves like a single click, which is the honest answer: the gesture had no
/// target, and inventing one would move the caret somewhere the user did not point.
/// </para>
/// <para>
/// Nothing about the model changes, so the outcome is <see cref="EditOutcome.Unchanged"/> and no undo
/// step is created - a selection is view state, exactly as it is for <see cref="SelectAllAction"/>.
/// </para>
/// </remarks>
public sealed class SelectValueAction : EditAction
{
    /// <inheritdoc />
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return EditResult.Unchanged(SelectionRanges.OfProperty(context) ?? context.Selection);
    }

    public override string ToString() => "select value";
}
