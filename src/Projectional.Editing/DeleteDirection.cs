namespace Projectional.Editing;

/// <summary>
/// Which way a delete reaches from the caret.
/// </summary>
/// <remarks>
/// Backspace and Delete are one action with a direction, because they are one operation: removing the
/// character next to the caret. Keeping them apart would duplicate the range arithmetic and the
/// validity check, and the two would eventually disagree about what happens at a cell boundary.
/// </remarks>
public enum DeleteDirection
{
    /// <summary>Removes the character before the caret, like Backspace.</summary>
    Backward,

    /// <summary>Removes the character after the caret, like Delete.</summary>
    Forward,
}
