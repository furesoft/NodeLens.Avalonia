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
/// Control is the host's as well, with a short list of exceptions. Ctrl+Left and Ctrl+Right, because
/// the arrows move a cell at a time and the character step has to stay reachable from the keyboard.
/// Ctrl+Space, which is the completion gesture in every editor that has one. Ctrl+A, Ctrl+C, Ctrl+X and
/// Ctrl+V, the four text gestures a user reaches for without looking and every platform spells the same
/// way - a clipboard that only worked through a context menu would be a clipboard nobody finds.
/// Ctrl+Home and Ctrl+End, because a click elsewhere in the window means "the start or end of this
/// document" in the host too, so taking them costs nothing. Alt has its own exceptions, Alt+Up and
/// Alt+Down, because reordering a node is an edit of the model and no host can offer it for the editor;
/// the combination is also the one every editor uses for the same job, so taking it is what keeps the
/// shortcut where a user looks for it. Alt+Left, Alt+Right and every other Alt combination stay with the
/// host, and so does Ctrl+PageUp/Ctrl+PageDown, which most browsers and tabbed applications own.
/// </para>
/// <para>
/// Alt+Enter and Ctrl+. are the quick-fix gesture, spelled the two ways the two editors this one is
/// modelled on spell it, and they are taken on purpose rather than fallen into: a user who learns the
/// gesture on day one must not have it moved later, and no host can offer a repair for a model it cannot
/// see. Both spellings are one <see cref="EditorKey"/>, so a host that binds its own menu shows the same
/// command twice rather than two commands that happen to do one thing.
/// </para>
/// </remarks>
public static class EditorKeyMap
{
    /// <summary>The editor key <paramref name="key"/> stands for, or <see langword="null"/> for none.</summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">The modifiers held while it was pressed.</param>
    public static EditorKey? Map(Key key, KeyModifiers modifiers)
    {
        var control = (modifiers & KeyModifiers.Control) != 0;

        if ((modifiers & KeyModifiers.Meta) != 0)
        {
            return null;
        }

        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            return control
                ? null
                : key switch
                {
                    Key.Up => EditorKey.AltUp,
                    Key.Down => EditorKey.AltDown,
                    Key.Enter => EditorKey.QuickFix,
                    _ => null,
                };
        }

        if (control && key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.Space or Key.A or Key.C or Key.V or Key.X or Key.OemPeriod))
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
            Key.Home => control ? EditorKey.ControlHome : EditorKey.Home,
            Key.End => control ? EditorKey.ControlEnd : EditorKey.End,
            Key.PageUp => EditorKey.PageUp,
            Key.PageDown => EditorKey.PageDown,
            Key.Tab => shifted ? EditorKey.ShiftTab : EditorKey.Tab,
            Key.Enter => EditorKey.Enter,
            Key.Back => EditorKey.Backspace,
            Key.Delete => EditorKey.Delete,
            Key.Space when control => EditorKey.ControlSpace,
            Key.A when control => EditorKey.SelectAll,
            Key.C when control => EditorKey.Copy,
            Key.X when control => EditorKey.Cut,
            Key.V when control => EditorKey.Paste,
            Key.OemPeriod when control => EditorKey.QuickFix,
            Key.Escape => EditorKey.Escape,
            _ => null,
        };
    }
}
