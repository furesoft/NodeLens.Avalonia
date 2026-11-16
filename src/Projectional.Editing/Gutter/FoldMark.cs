using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Where one fold arrow goes, and which way it points.</summary>
/// <param name="Node">The node the mark folds or unfolds.</param>
/// <param name="Bounds">The glyph's box, in layout units, on the node's first row.</param>
/// <param name="IsCollapsed">Whether the node is folded, which decides which glyph is drawn.</param>
/// <remarks>
/// The box is a square as tall as one line, so the arrow lines up with the text it belongs to rather than
/// with the number of the line - the two gutter columns stay independent.
/// </remarks>
public readonly record struct FoldMark(NodeId Node, LayoutRect Bounds, bool IsCollapsed)
{
    /// <summary>Whether <paramref name="x"/> and <paramref name="y"/> fall inside this mark.</summary>
    /// <param name="x">The x coordinate, in the same units as <see cref="Bounds"/>.</param>
    /// <param name="y">The y coordinate, in the same units as <see cref="Bounds"/>.</param>
    public bool Contains(double x, double y) => Bounds.Contains(x, y);
}
