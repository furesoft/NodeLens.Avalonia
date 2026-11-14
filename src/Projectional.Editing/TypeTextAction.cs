using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Types text into the property the caret is in, replacing the selection when there is one.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of text entry: there is no separate "insert", "overwrite" or "replace" action,
/// because in a projectional editor the text of a property is the property's value, and typing is
/// therefore always a replacement of a range - of the selection if there is one, of the empty range at
/// the caret if there is not. The candidate value is built as a string and handed to the model, which
/// validates it, so a character that is not part of the property's type is refused rather than stored
/// and repaired later.
/// </para>
/// <para>
/// Typing only works inside one property cell. Every other caret stop - a keyword, a space, an empty
/// child slot, a node with no notation - leaves the model alone, because there is no such thing as
/// editing the text of a keyword: the notation owns it. That is a rejection the user should not see as
/// an error, so it is reported as unchanged rather than rejected.
/// </para>
/// </remarks>
public sealed class TypeTextAction : EditAction
{
    /// <summary>Creates the action for one burst of typed text.</summary>
    /// <param name="text">The characters to type; may be empty, which is a no-op.</param>
    public TypeTextAction(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
    }

    /// <summary>The characters to type.</summary>
    public string Text { get; }

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

        var (start, end) = selection.HasText
            ? (selection.Start, selection.End)
            : (Math.Clamp(caret.Offset, 0, cell.Text.Length), Math.Clamp(caret.Offset, 0, cell.Text.Length));

        var value = string.Concat(cell.Text.AsSpan(0, start), Text.AsSpan(), cell.Text.AsSpan(end));

        if (!context.SetProperty(cell, value))
        {
            return EditResult.Rejected(selection, $"'{value}' is not a valid {cell.Type.Name}.");
        }

        return EditResult.Applied(Selection.At(CaretPosition.At(caret.Address, start + Text.Length)));
    }

    /// <summary>
    /// One key per place, so that a burst of typing in one property collapses into one undo step.
    /// </summary>
    /// <remarks>
    /// The place rather than the value: two keystrokes that both change the same property belong
    /// together, while a keystroke in another property is a different edit that deserves its own press
    /// of undo.
    /// </remarks>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return KeyOf(context.Selection.Caret.Address);
    }

    public override string ToString() => $"type \"{Text}\"";
}
