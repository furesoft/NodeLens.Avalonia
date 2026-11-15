namespace Projectional.Editing;

/// <summary>
/// Selects the word the caret stands on inside the editable cell it stands in.
/// </summary>
/// <remarks>
/// <para>
/// The gesture is a double click, and the unit is the word rather than the value: <c>total</c> out of
/// <c>total amount</c> is what the user pointed at, and the next keystroke should replace that and leave
/// the rest. <see cref="SelectValueAction"/> is the same gesture one click further out, which is why the
/// two are separate actions rather than one action with a width: a host that wants a different mapping
/// binds them differently, and nothing here has to know how often the button was pressed.
/// </para>
/// <para>
/// A cell that is not editable, or one whose value is empty, has no word to select, so the selection stays
/// exactly where the press put it. Widening to the value or to the node here would answer a question the
/// user did not ask - that widening is <see cref="SelectAllAction"/>'s ladder, and walking it is the
/// user's decision.
/// </para>
/// <para>
/// Nothing about the model changes, so the outcome is <see cref="EditOutcome.Unchanged"/> and no undo step
/// is created - a selection is view state, exactly as it is for <see cref="SelectAllAction"/>.
/// </para>
/// </remarks>
public sealed class SelectWordAction : EditAction
{
    /// <inheritdoc />
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return EditResult.Unchanged(SelectionRanges.OfWord(context) ?? context.Selection);
    }

    public override string ToString() => "select word";
}
