namespace Projectional.Editing;

/// <summary>
/// Moves the caret without changing the model.
/// </summary>
/// <remarks>
/// Moving is an action like any other so that the keyboard has one shape: the host maps a key to an
/// action, applies it, and repaints if the model changed. A move that had to be special-cased in the
/// host would be the first place where "everything is an edit action" stopped being true.
/// </remarks>
public sealed class MoveCaretAction : EditAction
{
    /// <summary>Creates the action for one move.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="move"/> is not a defined move.</exception>
    public MoveCaretAction(CaretMove move)
    {
        if (!Enum.IsDefined(move))
        {
            throw new ArgumentOutOfRangeException(nameof(move), move, "Unknown caret move.");
        }

        Move = move;
    }

    /// <summary>Which move to make.</summary>
    public CaretMove Move { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Always <see cref="EditOutcome.Unchanged"/>: the model is untouched, so the session keeps its tree
    /// and layout and only takes the new caret. A move that has nowhere to go returns the caret it was
    /// given, which is why this action cannot fail.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var caret = CaretNavigation.Move(context.Layout, context.Measure, context.Selection.Caret, Move);

        return EditResult.Unchanged(Selection.At(caret));
    }

    public override string ToString() => $"move {Move}";
}
