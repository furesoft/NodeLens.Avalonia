using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Projectional.Screenshots;

/// <summary>Turns the live control into the cropped, bordered PNG the README embeds.</summary>
/// <remarks>
/// The picture is rendered from the control itself, so it cannot drift from what the editor draws. Only
/// framing happens here: the document's own bounds are found by looking for pixels that differ from the
/// page background, which keeps an image tight even when the control was made larger so that a list or
/// a tooltip would fit.
/// </remarks>
internal static class ImageWriter
{
    /// <summary>How much of the border the existing documentation images have.</summary>
    private const int Border = 2;

    /// <summary>How far a pixel may differ from the page colour and still count as page.</summary>
    private const int BackgroundTolerance = 3;

    /// <summary>Renders <paramref name="visual"/> at its arranged size.</summary>
    internal static RenderTargetBitmap Render(Visual visual)
    {
        var bounds = visual.Bounds;

        var size = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(bounds.Height)));

        var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(visual);

        return bitmap;
    }

    /// <summary>Writes <paramref name="source"/> cropped to its content and framed with a border.</summary>
    /// <remarks>
    /// The gutter is treated as page for the vertical crop: it is painted the control's full height, so
    /// counting it as content would put a tall empty margin under every image. The left edge is kept, so
    /// an image still shows the margin the document is drawn against.
    /// </remarks>
    internal static PixelSize Save(
        RenderTargetBitmap source,
        string path,
        Color background,
        Color gutter,
        Color border)
    {
        var (pixels, size) = Pixels(source);

        var content = ContentBounds(pixels, size, background, gutter);
        var frame = new PixelSize(content.Width + (2 * Border), content.Height + (2 * Border));

        using var output = new RenderTargetBitmap(frame, new Vector(96, 96));

        using (var context = output.CreateDrawingContext())
        {
            context.DrawImage(
                source,
                new Rect(content.X, content.Y, content.Width, content.Height),
                new Rect(Border, Border, content.Width, content.Height));

            var pen = new Pen(new SolidColorBrush(border), Border);

            context.DrawRectangle(null, pen, new Rect(Border / 2.0, Border / 2.0, frame.Width - Border, frame.Height - Border));
        }

        using (var stream = File.Create(path))
        {
            output.Save(stream, PngBitmapEncoderOptions.Default);
        }

        return frame;
    }

    /// <summary>Writes <paramref name="source"/> exactly as it was rendered.</summary>
    /// <remarks>
    /// A window is its own frame - the menu bar at the top and the status bar at the bottom are the picture -
    /// so cropping it to the pixels that differ from a background would cut away what the shot is about.
    /// </remarks>
    internal static PixelSize Write(RenderTargetBitmap source, string path)
    {
        using (var stream = File.Create(path))
        {
            source.Save(stream, PngBitmapEncoderOptions.Default);
        }

        return source.PixelSize;
    }

    /// <summary>The rectangle holding the document: full width, and the rows that are not page.</summary>
    private static PixelRect ContentBounds(byte[] pixels, PixelSize size, Color background, Color gutter)
    {
        var minY = size.Height;
        var maxY = -1;
        var maxX = -1;

        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var offset = ((y * size.Width) + x) * 4;

                if (IsPage(pixels, offset, background) || IsPage(pixels, offset, gutter))
                {
                    continue;
                }

                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                maxX = Math.Max(maxX, x);
            }
        }

        // A control that painted nothing but its background still has to produce a usable image, so an
        // empty content answer falls back to the whole bitmap rather than a zero-sized crop.
        return maxY < minY
            ? new PixelRect(0, 0, size.Width, size.Height)
            : new PixelRect(0, minY, maxX + 1, (maxY - minY) + 1);
    }

    /// <summary>Whether the pixel at <paramref name="offset"/> is <paramref name="colour"/>.</summary>
    private static bool IsPage(byte[] pixels, int offset, Color colour) =>
        Distance(pixels[offset + 2], pixels[offset + 1], pixels[offset], colour) <= BackgroundTolerance;

    /// <summary>The bitmap's pixels in BGRA order, top row first.</summary>
    private static (byte[] Pixels, PixelSize Size) Pixels(RenderTargetBitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return (buffer, size);
    }

    /// <summary>How far apart two colours are, measured on their widest channel.</summary>
    private static int Distance(int red, int green, int blue, Color colour) => Math.Max(
        Math.Abs(red - colour.R),
        Math.Max(Math.Abs(green - colour.G), Math.Abs(blue - colour.B)));
}
