using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One decoration the frame has to draw, in layout coordinates.
/// </summary>
/// <remarks>
/// A frame is a display list: rectangles to fill and lines to stroke, then text on top. A decoration is the same
/// idea one level down from a text run - the cell already carries what it means, and the painter already knows how
/// to turn a meaning into a colour, so the frame only has to say where the region is and what it asks for. That is
/// what keeps the renderer generic: it draws a fill and a border without knowing whether the cell is a table, a
/// heading or a warning box.
/// </remarks>
/// <param name="Cell">The cell whose decoration this is.</param>
/// <param name="Bounds">The region the decoration covers, in layout coordinates.</param>
/// <param name="Paint">What the region asks for.</param>
public readonly record struct PlacedPaint(EditorCell Cell, LayoutRect Bounds, CellPaint Paint)
{
    /// <summary>The place in the model this decoration belongs to.</summary>
    public CellAddress Address => Cell.Address;

    public override string ToString() => $"{Paint} at {Bounds}";
}
