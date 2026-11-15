using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Turns a key into the edit action it stands for.
/// </summary>
/// <remarks>
/// <para>
/// The routing table is a public function rather than a method on the session so that a host can ask
/// what a key would do without a document open - to build a menu, to describe a shortcut, or to decide
/// whether to swallow the key at all. It is the single place where the mapping lives, so a control that
/// embeds the editor never has to reimplement it.
/// </para>
/// <para>
/// An unknown key throws. That is intended: the enum lists exactly what the layer handles, so a value
/// outside it means the caller either cast something it should not have or has a newer key than this
/// table, and both are programming errors that a silent <see langword="null"/> would hide.
/// </para>
/// </remarks>
public static class KeyRouter
{
    /// <summary>The action <paramref name="key"/> stands for.</summary>
    /// <remarks>
    /// Enter is routed as an insertion here, which is what the key does wherever the caret is not in a
    /// value of text. A host with a document open should ask <see cref="For(EditorSession, EditorKey)"/>
    /// instead, because only that one can tell the two cases apart.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is not a routed key.</exception>
    public static EditAction For(EditorKey key) => key switch
    {
        EditorKey.Left => new MoveCaretAction(CaretMove.Left),
        EditorKey.Right => new MoveCaretAction(CaretMove.Right),
        EditorKey.ControlLeft => new MoveCaretAction(CaretMove.LeftCharacter),
        EditorKey.ControlRight => new MoveCaretAction(CaretMove.RightCharacter),
        EditorKey.Up => new MoveCaretAction(CaretMove.Up),
        EditorKey.Down => new MoveCaretAction(CaretMove.Down),
        EditorKey.Home => new MoveCaretAction(CaretMove.LineStart),
        EditorKey.End => new MoveCaretAction(CaretMove.LineEnd),
        EditorKey.Tab => new TabAction(),
        EditorKey.ShiftTab => new TabAction(backward: true),
        EditorKey.Enter => new InsertNodeAction(),
        EditorKey.Backspace => new DeleteTextAction(DeleteDirection.Backward),
        EditorKey.Delete => new DeleteTextAction(DeleteDirection.Forward),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown editor key."),
    };

    /// <summary>The action <paramref name="key"/> stands for, given where the caret currently is.</summary>
    /// <param name="session">The session whose caret decides what Enter means.</param>
    /// <param name="key">The key that was pressed.</param>
    /// <remarks>
    /// Enter is the only key that is not decided by the key alone: in the text of a property it opens a
    /// line, and everywhere else it inserts a node. The distinction needs the caret, so a host that has
    /// a document open uses this overload and a host that does not uses the one above.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is not a routed key.</exception>
    public static EditAction For(EditorSession session, EditorKey key)
    {
        ArgumentNullException.ThrowIfNull(session);

        return key == EditorKey.Enter && IsTextPlace(session) ? new InsertLineBreakAction() : For(key);
    }

    /// <summary>Whether the caret of <paramref name="session"/> stands in text that can hold a line.</summary>
    /// <remarks>
    /// Editable is not enough: an integer or a date is typed but not broken, and a break in one of those
    /// would be a rejected edit the user never asked for. The property has to be a text, which is the
    /// only type whose value can contain a line.
    /// </remarks>
    private static bool IsTextPlace(EditorSession session) =>
        CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret)?.Cell is PropertyCell cell
        && cell.Type.Equals(PropertyType.String);

    /// <summary>The action for typed text, which is one burst of characters.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static EditAction Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new TypeTextAction(text);
    }
}
