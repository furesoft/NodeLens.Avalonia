namespace Projectional.Editing;

/// <summary>
/// One way of moving the caret inside the tree, named for its intent rather than for a key.
/// </summary>
/// <remarks>
/// <para>
/// The values are the moves the editor supports, not the keys that produce them: the same move is
/// reachable from a key, from a click and from a test, and a key map that wants to bind Home to
/// something else should not need a new action. Up and Down are the ones that need the layout, because
/// "the cell above" is a fact about the page and not about the tree; the page moves need the layout and
/// the height of the visible area as well, which is why the caller hands that number to
/// <see cref="CaretNavigation.Move"/> instead of it being derivable from the tree.
/// </para>
/// <para>
/// Horizontal motion is by cell, not by character, because a projectional editor shows a structure
/// rather than a text buffer: one press moves the caret onto the neighbouring cell and the reader walks
/// the notation the way the language spells it. The character moves are the ones that stay inside a
/// cell - they are what an arrow does in a text editor, and they remain reachable so that a typo in the
/// middle of a property can still be fixed without the mouse.
/// </para>
/// </remarks>
public enum CaretMove
{
    /// <summary>To the previous cell, at the end of its text.</summary>
    Left,

    /// <summary>To the next cell, at the start of its text.</summary>
    Right,

    /// <summary>One character towards the start, crossing into the previous cell at the start of one.</summary>
    LeftCharacter,

    /// <summary>One character towards the end, crossing into the next cell at the end of one.</summary>
    RightCharacter,

    /// <summary>To the closest caret stop on the line above, keeping the column.</summary>
    Up,

    /// <summary>To the closest caret stop on the line below, keeping the column.</summary>
    Down,

    /// <summary>To the closest caret stop one page of lines above, keeping the column.</summary>
    /// <remarks>
    /// A page is the height of the visible area, which the layout does not know and the host therefore
    /// supplies; a document at least a page tall is what makes this different from <see cref="Up"/>.
    /// </remarks>
    PageUp,

    /// <summary>To the closest caret stop one page of lines below, keeping the column.</summary>
    PageDown,

    /// <summary>To the start of the line the caret is on.</summary>
    LineStart,

    /// <summary>To the end of the line the caret is on.</summary>
    LineEnd,

    /// <summary>To the very first caret stop of the document.</summary>
    DocumentStart,

    /// <summary>To the very last caret stop of the document.</summary>
    DocumentEnd,
}
