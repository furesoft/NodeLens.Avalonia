using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One picture the frame has to draw, in layout coordinates.
/// </summary>
/// <remarks>
/// A picture travels as a region and a name, never as an image: the editing layer has no reference to Avalonia, and
/// a frame that carried a bitmap would tie the display list to one frontend. The name is the key the notation wrote;
/// resolving it into artwork is the host's job, exactly as resolving a
/// <see cref="Projectional.Language.PaintRole"/> into a brush is.
/// </remarks>
/// <param name="Cell">The cell the picture belongs to, which is what a host looks a click up by.</param>
/// <param name="Bounds">Where the picture goes, in layout coordinates.</param>
/// <param name="Key">The name of the picture, which the host resolves - never a path.</param>
public readonly record struct PlacedImage(EditorCell Cell, LayoutRect Bounds, string Key)
{
    /// <summary>The place in the model this picture belongs to.</summary>
    public CellAddress Address => Cell.Address;

    public override string ToString() => $"image {Key} at {Bounds}";
}
