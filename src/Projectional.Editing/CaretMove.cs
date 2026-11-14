namespace Projectional.Editing;

/// <summary>
/// One way of moving the caret inside the tree, named for its intent rather than for a key.
/// </summary>
/// <remarks>
/// The values are the moves the editor supports, not the keys that produce them: the same move is
/// reachable from a key, from a click and from a test, and a key map that wants to bind Home to
/// something else should not need a new action. Up and Down are the ones that need the layout, because
/// "the cell above" is a fact about the page and not about the tree.
/// </remarks>
public enum CaretMove
{
    /// <summary>One character towards the start, crossing into the previous cell at the start of one.</summary>
    Left,

    /// <summary>One character towards the end, crossing into the next cell at the end of one.</summary>
    Right,

    /// <summary>To the closest caret stop on the line above, keeping the column.</summary>
    Up,

    /// <summary>To the closest caret stop on the line below, keeping the column.</summary>
    Down,

    /// <summary>To the start of the line the caret is on.</summary>
    LineStart,

    /// <summary>To the end of the line the caret is on.</summary>
    LineEnd,

    /// <summary>To the very first caret stop of the document.</summary>
    DocumentStart,

    /// <summary>To the very last caret stop of the document.</summary>
    DocumentEnd,
}
