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

    /// <summary>Ctrl+Home, which goes to the very first caret stop of the document.</summary>
    ControlHome,

    /// <summary>End.</summary>
    End,

    /// <summary>Ctrl+End, which goes to the very last caret stop of the document.</summary>
    ControlEnd,

    /// <summary>PageUp, which moves the caret one visible page of lines towards the start.</summary>
    PageUp,

    /// <summary>PageDown, which moves the caret one visible page of lines towards the end.</summary>
    PageDown,

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

    /// <summary>Ctrl+A, which widens the selection: the value, then the node, then the document.</summary>
    SelectAll,

    /// <summary>Ctrl+C, which copies the selected text.</summary>
    Copy,

    /// <summary>Ctrl+X, which copies the selected text and deletes it.</summary>
    Cut,

    /// <summary>Ctrl+V, which inserts the clipboard text at the caret, replacing any selection.</summary>
    Paste,

    /// <summary>Ctrl+B, which goes to the declaration the reference at the caret points at.</summary>
    GoToDefinition,

    /// <summary>Alt+Enter and Ctrl+., which offer the repairs for the line the caret is on.</summary>
    /// <remarks>
    /// One value for two gestures, because it is one meaning with two spellings: Alt+Enter is MPS' and
    /// Ctrl+. is Visual Studio's, and a user who has learned either should find the repairs where they
    /// expect them. The editor applies them as one undo step, so both gestures can be taken from the host
    /// without taking away anything a host could have done with them.
    /// </remarks>
    QuickFix,

    /// <summary>Ctrl+D, which copies the node the caret is in right after it.</summary>
    /// <remarks>
    /// The operation is not new - <see cref="DuplicateNodeAction"/> is what the context menu and the intention list
    /// already offer - so this value exists to give it the key every structural editor spends on it. Taken from the
    /// host for the same reason Ctrl+B is: duplicating a node is an edit of a model no host can see.
    /// </remarks>
    Duplicate,

    /// <summary>The Insert key, which adds an empty sibling of the caret's node before it.</summary>
    /// <remarks>
    /// Before, because Enter is the after: <see cref="InsertNodeAction"/> inserts following the caret's node, so a
    /// gesture that also inserted afterwards would be Enter with another name. Insert is the one side Enter cannot
    /// reach, and it is the key MPS spends on it.
    /// </remarks>
    InsertBefore,
}
