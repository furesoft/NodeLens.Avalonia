using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One cell's text, what it means, and the rectangle it occupies, ready to be drawn.
/// </summary>
/// <remarks>
/// A run is what a renderer iterates: it holds no cell kind, so drawing never has to ask what kind of
/// cell it is looking at - the question the renderer would ask has already been answered and reduced to
/// a <see cref="TextStyleKind"/>. The bounds are in layout coordinates, like everything else the
/// projection layer produces, and the viewport offsets them on the way to the screen.
/// </remarks>
public readonly record struct TextRun(
    EditorCell Cell,
    string Text,
    LayoutRect Bounds,
    TextStyleKind Style)
{
    /// <summary>The address of the cell this run draws.</summary>
    public CellAddress Address => Cell.Address;

    public override string ToString() => $"{Address} '{Text}' at {Bounds} as {Style}";
}
