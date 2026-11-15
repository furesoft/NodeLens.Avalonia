namespace Projectional.Editing;

/// <summary>
/// The keys the editor itself handles, named for the physical key rather than for a character.
/// </summary>
/// <remarks>
/// The editor takes keys, not characters, because which action a key stands for is a policy the layer
/// owns and the host does not: Enter opens a line in a value of text and inserts a node elsewhere,
/// Backspace deletes text
/// only where there is text, and Tab moves between slots instead of focus. Typed text is not a key -
/// it arrives as a string, one burst per keystroke or per input method - so it does not appear here.
/// Keys the layer does not handle are simply not in this list, which is how the host knows to keep them
/// for itself; adding one of those mappings later is a change to <see cref="KeyRouter"/>, not to this
/// type.
/// </remarks>
public enum EditorKey
{
    /// <summary>The left arrow.</summary>
    Left,

    /// <summary>The right arrow.</summary>
    Right,

    /// <summary>Ctrl+Left, which steps one character instead of one cell.</summary>
    ControlLeft,

    /// <summary>Ctrl+Right, which steps one character instead of one cell.</summary>
    ControlRight,

    /// <summary>The up arrow.</summary>
    Up,

    /// <summary>The down arrow.</summary>
    Down,

    /// <summary>Alt+Up, which moves the node the caret is in one place towards the front of its slot.</summary>
    AltUp,

    /// <summary>Alt+Down, which moves the node the caret is in one place towards the end of its slot.</summary>
    AltDown,

    /// <summary>Home.</summary>
    Home,

    /// <summary>End.</summary>
    End,

    /// <summary>Tab.</summary>
    Tab,

    /// <summary>Shift+Tab.</summary>
    ShiftTab,

    /// <summary>Enter, which opens a line in text and inserts a node everywhere else.</summary>
    Enter,

    /// <summary>Backspace.</summary>
    Backspace,

    /// <summary>The Delete key.</summary>
    Delete,

    /// <summary>Ctrl+Space, which offers the concepts that could fill the empty slot at the caret.</summary>
    ControlSpace,

    /// <summary>Escape, which closes an open completion list.</summary>
    Escape,

    /// <summary>Ctrl+A, which selects the whole text of the cell at the caret.</summary>
    SelectAll,

    /// <summary>Ctrl+C, which copies the selected text.</summary>
    Copy,

    /// <summary>Ctrl+X, which copies the selected text and deletes it.</summary>
    Cut,

    /// <summary>Ctrl+V, which inserts the clipboard text at the caret, replacing any selection.</summary>
    Paste,
}
