namespace Projectional.Projection;

/// <summary>How a container arranges its content, which is the only layout decision a tree makes.</summary>
/// <remarks>
/// Orientation lives on the container rather than on the cell being arranged, so that the same cell
/// can be stacked in one place and run on a line in another. This is what lets a one-child slot
/// declared inline hold a child whose own notation is a stack of lines: the child's root container is
/// vertical, and the line around it breaks instead of pretending it fits.
/// </remarks>
public enum CellOrientation
{
    /// <summary>Content runs along one line.</summary>
    Horizontal,

    /// <summary>Content runs one item per line.</summary>
    Vertical,
}
