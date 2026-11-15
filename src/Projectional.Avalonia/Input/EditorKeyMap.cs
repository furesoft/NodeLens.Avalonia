using Avalonia.Input;

using Projectional.Editing;

namespace Projectional.Avalonia;

/// <summary>
/// Maps the keys of a window onto the keys the editing layer handles.
/// </summary>
/// <remarks>
/// <para>
/// The control does not decide what a key means - <see cref="KeyRouter"/> does, once, for every host.
/// What is left for the windowing layer is the translation from physical keys to that vocabulary,
/// which is exactly what this type is: a table from Avalonia's <see cref="Key"/> to
/// <see cref="EditorKey"/>, and <see langword="null"/> for everything else.
/// </para>
/// <para>
/// A key held with Alt or Meta is not the editor's, even when the bare key is: those combinations
/// belong to the host - Alt+Left is a back navigation in many applications - and swallowing them
/// because the plain key happens to be routed would take shortcuts away from the very application that
/// embeds the editor. Shift is part of what the editor does, because Shift+Tab walks slots backwards.
/// Control is the host's as well, with one exception: Ctrl+Left and Ctrl+Right, because the arrows move
/// a cell at a time and the character step has to stay reachable from the keyboard.
/// </para>
/// </remarks>
public static class EditorKeyMap
{
    /// <summary>The editor key <paramref name="key"/> stands for, or <see langword="null"/> for none.</summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">The modifiers held while it was pressed.</param>
    public static EditorKey? Map(Key key, KeyModifiers modifiers)
    {
        if ((modifiers & (KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return null;
        }

        var control = (modifiers & KeyModifiers.Control) != 0;

        if (control && key is not (Key.Left or Key.Right))
        {
            return null;
        }

        var shifted = (modifiers & KeyModifiers.Shift) != 0;

        return key switch
        {
            Key.Left => control ? EditorKey.ControlLeft : EditorKey.Left,
            Key.Right => control ? EditorKey.ControlRight : EditorKey.Right,
            Key.Up => EditorKey.Up,
            Key.Down => EditorKey.Down,
            Key.Home => EditorKey.Home,
            Key.End => EditorKey.End,
            Key.Tab => shifted ? EditorKey.ShiftTab : EditorKey.Tab,
            Key.Enter => EditorKey.Enter,
            Key.Back => EditorKey.Backspace,
            Key.Delete => EditorKey.Delete,
            _ => null,
        };
    }
}
