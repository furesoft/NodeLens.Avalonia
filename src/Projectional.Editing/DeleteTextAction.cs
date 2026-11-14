using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Deletes the selection, or one character next to the caret.
/// </summary>
/// <remarks>
/// <para>
/// Deleting is a text edit like typing and is refused the same way: the candidate value has to be valid
/// for the property's type, so removing the last digit of a number is rejected instead of producing an
/// empty number. There is no way to unset a property in the model, and inventing one here would be the
/// editing layer deciding what an empty integer means - a question that belongs to the concept.
/// </para>
/// <para>
/// At a cell boundary the action does nothing. Backspace at the start of a property could delete
/// backwards into the previous cell, but "delete the previous keyword" is not a deletion at all - the
/// notation owns that text - so the honest answer is that there is nothing there to delete.
/// </para>
/// </remarks>
public sealed class DeleteTextAction : EditAction
{
    /// <summary>Creates the action for one delete.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a defined direction.</exception>
    public DeleteTextAction(DeleteDirection direction)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown delete direction.");
        }

        Direction = direction;
    }

    /// <summary>Which way the delete reaches.</summary>
    public DeleteDirection Direction { get; }

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;
        var caret = selection.Caret;

        if (context.PlaceAt(caret)?.Cell is not PropertyCell cell)
        {
            return EditResult.Unchanged(selection);
        }

        var offset = Math.Clamp(caret.Offset, 0, cell.Text.Length);

        var (start, end) = selection.HasText
            ? (selection.Start, selection.End)
            : Direction == DeleteDirection.Backward
                ? (offset - 1, offset)
                : (offset, offset + 1);

        if (start < 0 || end > cell.Text.Length)
        {
            return EditResult.Unchanged(selection);
        }

        var value = string.Concat(cell.Text.AsSpan(0, start), cell.Text.AsSpan(end));

        if (!context.SetProperty(cell, value))
        {
            return EditResult.Rejected(selection, $"'{value}' is not a valid {cell.Type.Name}.");
        }

        return EditResult.Applied(Selection.At(CaretPosition.At(caret.Address, start)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Holding Backspace is one burst of deletion in one property, so it collapses the same way typing
    /// does. Deleting a selection is not part of such a burst: it is a single deliberate act on a range
    /// the user chose, and merging it with the keystrokes that follow would make undo skip past it.
    /// </remarks>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Selection.HasText ? null : KeyOf(context.Selection.Caret.Address);
    }

    public override string ToString() => $"delete {Direction}";
}
