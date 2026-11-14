namespace Projectional.Editing;

/// <summary>
/// The keys the editor itself handles, named for the physical key rather than for a character.
/// </summary>
/// <remarks>
/// The editor takes keys, not characters, because which action a key stands for is a policy the layer
/// owns and the host does not: Enter inserts a node in a projectional editor, Backspace deletes text
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

    /// <summary>The up arrow.</summary>
    Up,

    /// <summary>The down arrow.</summary>
    Down,

    /// <summary>Home.</summary>
    Home,

    /// <summary>End.</summary>
    End,

    /// <summary>Tab.</summary>
    Tab,

    /// <summary>Shift+Tab.</summary>
    ShiftTab,

    /// <summary>Enter, which inserts rather than starting a new line.</summary>
    Enter,

    /// <summary>Backspace.</summary>
    Backspace,

    /// <summary>The Delete key.</summary>
    Delete,
}
