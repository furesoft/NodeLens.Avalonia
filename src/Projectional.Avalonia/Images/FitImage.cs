using Avalonia;
using Avalonia.Media;

namespace Projectional.Avalonia;

/// <summary>
/// A picture that keeps its shape: its own aspect ratio, centred in whatever box it is handed.
/// </summary>
/// <remarks>
/// <para>
/// The picture a notation asks for is drawn into a box the notation sized - an icon is as tall as the line, a
/// photograph is a box of its own - and those boxes are not the shape of the artwork. Stretching a path to fill the
/// box distorts it, and it distorts the stroke with it: a key drawn in a wide, flat bounding box came out as a blob
/// in a square box because the vertical scale was twice the horizontal one.
/// </para>
/// <para>
/// So the scale is the smaller of the two, applied to both axes, and the result is centred. That is what an icon
/// wants and what a photograph wants; a caller that really wants a stretched picture still has the inner image.
/// </para>
/// </remarks>
internal sealed class FitImage : IImage
{
    private readonly IImage _picture;

    /// <summary>Wraps a picture so it keeps its shape in any box.</summary>
    /// <param name="picture">The picture to draw.</param>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    public FitImage(IImage picture)
    {
        ArgumentNullException.ThrowIfNull(picture);

        _picture = picture;
    }

    /// <summary>The extent of the picture, which is the source rectangle a host expects.</summary>
    public Size Size => _picture.Size;

    /// <summary>Draws the picture at the largest scale that fits, centred in <paramref name="destRect"/>.</summary>
    /// <param name="context">The surface to paint on.</param>
    /// <param name="sourceRect">The part of the picture wanted, usually all of it.</param>
    /// <param name="destRect">Where it goes, in the surface's coordinates.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(destRect.Width / sourceRect.Width, destRect.Height / sourceRect.Height);
        var width = sourceRect.Width * scale;
        var height = sourceRect.Height * scale;

        _picture.Draw(
            context,
            sourceRect,
            new Rect(
                destRect.X + ((destRect.Width - width) / 2),
                destRect.Y + ((destRect.Height - height) / 2),
                width,
                height));
    }

    /// <inheritdoc />
    public override string ToString() => $"fitted {_picture}";
}
