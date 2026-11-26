using Avalonia;
using Avalonia.Media;

namespace Projectional.Avalonia;

/// <summary>
/// A vector picture that draws a path into whatever rectangle it is handed.
/// </summary>
/// <remarks>
/// <para>
/// This is what <see cref="CompletionIcon.FromPath"/> builds, and it exists because Avalonia's own
/// <see cref="DrawingImage"/> does not survive being drawn more than once. A completion list draws one
/// icon per row into a single <see cref="DrawingContext"/>, and with a <see cref="DrawingImage"/> the
/// rows came out shifted a few units each and every row after the second came out blank - the drawing's
/// own transform is not restored between calls. Drawing the geometry here keeps the transform under this
/// type's control, so the tenth row looks exactly like the first.
/// </para>
/// <para>
/// The path's own bounds are the source rectangle, not the box it is drawn into. That is deliberate: it
/// lets a palette draw a small marker and a large diamond from the same code without the small one being
/// blown up to fill the row.
/// </para>
/// </remarks>
internal sealed class GeometryImage : IImage
{
    private readonly Geometry _geometry;
    private readonly IBrush? _fill;
    private readonly IPen? _pen;

    private readonly Point _origin;

    /// <summary>Creates the picture.</summary>
    /// <param name="geometry">The shape, in whatever coordinates it was authored.</param>
    /// <param name="fill">What the shape is filled with, or <see langword="null"/> to draw it as an outline.</param>
    /// <param name="pen">
    /// The outline, or <see langword="null"/> for a filled shape. An icon set is usually drawn as strokes - the
    /// shapes are open paths, and filling one paints the area between its ends rather than the line they describe -
    /// while a marker such as the completion list's diamond is a closed shape and is filled.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="geometry"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Neither a fill nor a pen was given.</exception>
    public GeometryImage(Geometry geometry, IBrush? fill = null, IPen? pen = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (fill is null && pen is null)
        {
            throw new ArgumentException(
                "A picture needs something to draw with: pass a fill, a pen, or both.",
                nameof(fill));
        }

        _geometry = geometry;
        _fill = fill;
        _pen = pen;
        _origin = geometry.Bounds.TopLeft;
        Size = geometry.Bounds.Size;
    }

    /// <summary>The extent of the shape, which is the source rectangle a host expects.</summary>
    public Size Size { get; }

    /// <summary>Draws the shape, scaled from <paramref name="sourceRect"/> into <paramref name="destRect"/>.</summary>
    /// <param name="context">The surface to paint on.</param>
    /// <param name="sourceRect">The part of the picture wanted, usually all of it.</param>
    /// <param name="destRect">Where it goes, in the surface's coordinates.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A path authored at any origin is first moved to (0, 0), so the scale that follows maps the shape
        // itself onto the box rather than onto the corner of its coordinate space.
        var scaleX = sourceRect.Width > 0 ? destRect.Width / sourceRect.Width : 1;
        var scaleY = sourceRect.Height > 0 ? destRect.Height / sourceRect.Height : 1;

        var transform =
            Matrix.CreateTranslation(-_origin.X, -_origin.Y)
            * Matrix.CreateScale(scaleX, scaleY)
            * Matrix.CreateTranslation(destRect.X - sourceRect.X, destRect.Y - sourceRect.Y);

        using (context.PushClip(destRect))
        using (context.PushTransform(transform))
        {
            context.DrawGeometry(_fill, _pen, _geometry);
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Size.Width} x {Size.Height} geometry";
}
