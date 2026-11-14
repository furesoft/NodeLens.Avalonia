namespace Projectional.Editing;

/// <summary>
/// Tab and Shift+Tab: the next or previous place the user can put something.
/// </summary>
/// <remarks>
/// <para>
/// Tab is not "the next cell": the reader sees keywords and spaces between the slots, and stopping on
/// them would make Tab take several presses to cross one line. It is not "the next slot in declaration
/// order" either, because a notation may show a concept's slots in any order it likes and the keyboard
/// has to follow what the user sees. What Tab visits is the slots in the order the notation shows them,
/// which is what makes the keyboard agree with the picture.
/// </para>
/// <para>
/// It stops at the ends instead of wrapping, so that a user holding Tab never ends up back where they
/// started without noticing, and so that the host can use "Tab did nothing" as the signal to move focus
/// out of the editor - the behaviour a control embedded in a host window needs.
/// </para>
/// </remarks>
public sealed class TabAction : EditAction
{
    /// <summary>Creates the action.</summary>
    /// <param name="backward">Whether to go to the previous slot instead of the next.</param>
    public TabAction(bool backward = false) => Backward = backward;

    /// <summary>Whether this is Shift+Tab.</summary>
    public bool Backward { get; }

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;
        var places = context.Places;
        var current = CaretPlaces.Resolve(places, selection.Caret);

        if (current < 0)
        {
            return EditResult.Unchanged(selection);
        }

        var step = Backward ? -1 : 1;

        for (var index = current + step; index >= 0 && index < places.Count; index += step)
        {
            if (places[index].IsSlot)
            {
                return EditResult.Unchanged(Selection.At(CaretPosition.At(places[index], 0)));
            }
        }

        return EditResult.Unchanged(selection);
    }

    public override string ToString() => Backward ? "previous slot" : "next slot";
}
