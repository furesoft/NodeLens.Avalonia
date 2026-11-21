using Avalonia.Input;

namespace Projectional.Workbench;

/// <summary>Which of the emulator's key names an Avalonia key is, if any.</summary>
/// <remarks>
/// A terminal sends two kinds of bytes: the characters the user typed, which come from the text input event, and the
/// sequences a key stands for, which come from the key. This map is the second half. It exists as a table rather than
/// inline in the control because "which keys a terminal has" is a fact about the terminal and not about Avalonia - and
/// a fact is worth a test that no window is needed for.
/// </remarks>
internal static class TerminalKeyMap
{
    /// <summary>The emulator's name for <paramref name="key"/>, or <see langword="null"/> for a key that has none.</summary>
    /// <param name="key">The key that was pressed.</param>
    /// <remarks>
    /// The names are VtNetCore's, which are the ones a terminal's own documentation uses. A key that is not in this
    /// table is not a key a terminal sends: a letter arrives as text input instead, and a modifier arrives as part of
    /// whatever key it is held with.
    /// </remarks>
    internal static string? NameOf(Key key) => key switch
    {
        Key.Enter => "Enter",
        Key.Tab => "Tab",
        Key.Escape => "Escape",
        Key.Back => "Back",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.Home => "Home",
        Key.End => "End",
        Key.Insert => "Insert",
        Key.Delete => "Delete",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",

        // The function keys are one range in Avalonia and one range in the emulator's table, so the name is the
        // number - written out rather than formatted, because the table is closed and a wrong width would send a
        // sequence nothing answers.
        Key.F1 => "F1",
        Key.F2 => "F2",
        Key.F3 => "F3",
        Key.F4 => "F4",
        Key.F5 => "F5",
        Key.F6 => "F6",
        Key.F7 => "F7",
        Key.F8 => "F8",
        Key.F9 => "F9",
        Key.F10 => "F10",
        Key.F11 => "F11",
        Key.F12 => "F12",
        _ => null,
    };

    /// <summary>The emulator's name for the letter a control chord turns into a control character.</summary>
    /// <param name="key">The pressed key.</param>
    /// <remarks>
    /// Ctrl+C is not a copy in a terminal - it is the break a running program is asked to stop on - so the chord is
    /// sent as the control character the emulator's table spells it with, and the shell's own Ctrl+C is what a
    /// <em>selection</em> copies.
    /// </remarks>
    internal static string? ControlLetterOf(Key key) =>
        key is >= Key.A and <= Key.Z ? ((char)('A' + (key - Key.A))).ToString() : null;
}
