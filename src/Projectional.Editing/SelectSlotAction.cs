using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Moves the caret into one slot and selects all of its text - the jump a rename intention makes.</summary>
/// <remarks>
/// <para>
/// It is a navigation, not an edit: nothing about the model changes, so the answer is
/// <see cref="EditResult.Unchanged"/> carrying the selection the caret should land on. That is the same shape
/// <see cref="MoveCaretAction"/> has, and it is what makes "rename" a plain offer rather than a refactoring - the
/// user types the new name, and no reference in another model can break because the action never touched one.
/// </para>
/// <para>
/// The slot is addressed by <see cref="CellAddress"/> rather than by node, because a rename names the value a user
/// sees: the caret has to land on that cell, and a cell is what the projection has an address for. An address that
/// no longer resolves is <see cref="EditResult.Rejected"/> with a reason, not an exception - an offer outlives the
/// document it was derived from.
/// </para>
/// </remarks>
public sealed class SelectSlotAction : EditAction
{
    private readonly CellAddress _slot;

    /// <summary>Selects the text of the cell <paramref name="slot"/> addresses.</summary>
    /// <param name="slot">The slot to land in, usually a node's name slot.</param>
    public SelectSlotAction(CellAddress slot) => _slot = slot;

    /// <summary>The slot this action lands in.</summary>
    public CellAddress Slot => _slot;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// An empty slot keeps a bare caret instead of selecting a zero-length range, because the two are the same
    /// thing to type over and a collapsed selection is what every other caret-only path produces.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (CaretNavigation.PlaceAt(context.Tree, CaretPosition.At(_slot)) is not { } place)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The cell {_slot} is not on screen, so there is nothing to select.");
        }

        var start = CaretPosition.At(place, 0);

        return EditResult.Unchanged(
            place.Length > 0
                ? new Selection(start, CaretPosition.At(place, place.Length))
                : Selection.At(start));
    }

    public override string ToString() => $"select {_slot}";
}
