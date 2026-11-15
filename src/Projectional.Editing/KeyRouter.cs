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

    /// <summary>The action for typed text, which is one burst of characters.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static EditAction Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new TypeTextAction(text);
    }
}
