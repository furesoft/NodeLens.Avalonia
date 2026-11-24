using Projectional.Editing;

namespace Projectional.Web;

/// <summary>
/// Translates a browser keyboard event into the editor's key vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// The browser's <c>KeyboardEvent.key</c> is not Avalonia's <c>Key</c>, so the desktop's table cannot be reused
/// verbatim - but the decisions behind it can, and are: Alt and Meta belong to the host, Control is the host's
/// except for the gestures a structural editor cannot give away, and Shift is part of what the editor does.
/// </para>
/// <para>
/// The type also answers the one question the page has to settle <em>before</em> the event is over: whether to
/// call <c>preventDefault</c>. That has to happen synchronously, so a page cannot wait for a .NET answer; it asks
/// <see cref="IsHandled"/> against the same table <see cref="Map"/> is built from. One table, two questions, so a
/// key can never be swallowed without being mapped.
/// </para>
/// </remarks>
public static class WebKeyMap
{
    private sealed record Binding(string Key, bool Control, bool Shift, bool Alt, EditorKey? Value);

    /// <summary>
    /// The chords the editor or the shell acts on. A <see langword="null"/> value is a shell command - undo,
    /// redo, save, new - that the page runs itself rather than one the session routes.
    /// </summary>
    private static readonly Binding[] Bindings =
    [
        new("ArrowLeft", false, false, false, EditorKey.Left),
        new("ArrowRight", false, false, false, EditorKey.Right),
        new("ArrowUp", false, false, false, EditorKey.Up),
        new("ArrowDown", false, false, false, EditorKey.Down),
        new("ArrowLeft", true, false, false, EditorKey.ControlLeft),
        new("ArrowRight", true, false, false, EditorKey.ControlRight),
        new("Home", false, false, false, EditorKey.Home),
        new("End", false, false, false, EditorKey.End),
        new("Home", true, false, false, EditorKey.ControlHome),
        new("End", true, false, false, EditorKey.ControlEnd),
        new("PageUp", false, false, false, EditorKey.PageUp),
        new("PageDown", false, false, false, EditorKey.PageDown),
        new("Tab", false, false, false, EditorKey.Tab),
        new("Tab", false, true, false, EditorKey.ShiftTab),
        new("Enter", false, false, false, EditorKey.Enter),
        new("Backspace", false, false, false, EditorKey.Backspace),
        new("Delete", false, false, false, EditorKey.Delete),
        new("Escape", false, false, false, EditorKey.Escape),
        new("Insert", false, false, false, EditorKey.InsertBefore),
        new("Space", true, false, false, EditorKey.ControlSpace),
        new("a", true, false, false, EditorKey.SelectAll),
        new("b", true, false, false, EditorKey.GoToDefinition),
        new("c", true, false, false, EditorKey.Copy),
        new("d", true, false, false, EditorKey.Duplicate),
        new("v", true, false, false, EditorKey.Paste),
        new("x", true, false, false, EditorKey.Cut),
        new("ArrowUp", false, false, true, EditorKey.AltUp),
        new("ArrowDown", false, false, true, EditorKey.AltDown),

        // The shell's own gestures. They are in this table so the page swallows them; the page is what runs them.
        new("z", true, false, false, null),
        new("y", true, false, false, null),
        new("z", true, true, false, null),
        new("s", true, false, false, null),
        new("n", true, false, false, null),
    ];

    /// <summary>Every chord the page must not hand to the browser.</summary>
    /// <remarks>
    /// Paste is deliberately absent: a browser only exposes another application's clipboard through the native
    /// <c>paste</c> event, which never fires for a key the page swallowed. Ctrl+V therefore reaches the text area and
    /// the page forwards what arrived, while <see cref="Map"/> still answers <see cref="EditorKey.Paste"/> for a menu
    /// entry that has no native gesture.
    /// </remarks>
    public static IReadOnlyCollection<string> HandledChords { get; } =
        [.. Bindings.Where(binding => binding.Value != EditorKey.Paste).Select(binding => Chord(binding))];

    /// <summary>The editor key a browser key stands for, or <see langword="null"/> when it stands for none.</summary>
    /// <param name="key">The browser's <c>KeyboardEvent.key</c>.</param>
    /// <param name="control">Whether Control was held.</param>
    /// <param name="shift">Whether Shift was held.</param>
    /// <param name="alt">Whether Alt was held.</param>
    /// <remarks>
    /// Meta is deliberately not a parameter: a Command combination belongs to the operating system or the browser
    /// on every platform that has one, and an editor that swallowed Cmd+C would break copy for the page around it.
    /// </remarks>
    public static EditorKey? Map(string key, bool control, bool shift, bool alt)
    {
        var chord = Chord(key, control, shift, alt);

        return Bindings.FirstOrDefault(binding => binding.Value is not null && Chord(binding) == chord)?.Value;
    }

    /// <summary>Whether the page must swallow the chord, whether or not the editor acts on it.</summary>
    /// <param name="key">The browser's <c>KeyboardEvent.key</c>.</param>
    /// <param name="control">Whether Control was held.</param>
    /// <param name="shift">Whether Shift was held.</param>
    /// <param name="alt">Whether Alt was held.</param>
    public static bool IsHandled(string key, bool control, bool shift, bool alt) =>
        HandledChords.Contains(Chord(key, control, shift, alt));

    /// <summary>The normalized spelling of one chord, the key both questions are asked with.</summary>
    /// <param name="key">The browser's <c>KeyboardEvent.key</c>.</param>
    /// <param name="control">Whether Control was held.</param>
    /// <param name="shift">Whether Shift was held.</param>
    /// <param name="alt">Whether Alt was held.</param>
    public static string Chord(string key, bool control, bool shift, bool alt)
    {
        var normalized = key == " " ? "Space"
            : key.Length == 1 ? key.ToLowerInvariant()
            : key;

        return string.Concat(
            control ? "ctrl+" : string.Empty,
            shift ? "shift+" : string.Empty,
            alt ? "alt+" : string.Empty,
            normalized);
    }

    private static string Chord(Binding binding) => Chord(binding.Key, binding.Control, binding.Shift, binding.Alt);
}
