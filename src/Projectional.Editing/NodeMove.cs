namespace Projectional.Editing;

/// <summary>
/// Which way a node moves among the children of its slot.
/// </summary>
/// <remarks>
/// The direction is named after the picture rather than after the list index, because that is what the
/// user presses: a node whose line is above another one moves up when it goes towards the front of the
/// list, however the notation happens to arrange the slot.
/// </remarks>
public enum NodeMove
{
    /// <summary>Towards the front of the slot.</summary>
    Up,

    /// <summary>Towards the end of the slot.</summary>
    Down,

    /// <summary>To the first place of the slot, however far that is.</summary>
    /// <remarks>
    /// A named extreme rather than "up, several times", because the catalogue's entry says where the node ends up:
    /// a caller that asked for the front and got one step would have to run the action repeatedly, which is several
    /// undo steps for one intention.
    /// </remarks>
    ToFront,

    /// <summary>To the last place of the slot, however far that is.</summary>
    ToBack,
}
