using Avalonia.Media;

namespace Projectional.Avalonia;

/// <summary>
/// The picture a completion row is marked with.
/// </summary>
/// <remarks>
/// <para>
/// An icon is an <see cref="IImage"/>, not a character. A glyph typed into a label depends on the font
/// the row happens to be shaped in - it can come out as a box, sit off the baseline, or change width
/// when the font falls back - whereas an image is drawn into a box the palette chose and looks the same
/// on every machine. It also means a host can hand over a picture it already owns: a <see cref="Bitmap"/>
/// decoded from a PNG, or anything else that implements <see cref="IImage"/>.
/// </para>
/// <para>
/// The built-in set is vector, so the repository commits no binary asset and the icons stay sharp on a
/// high-density display. <see cref="FromPath"/> builds one from an SVG-style path string, which is the
/// shape a language author would copy from an icon set; it is drawn by
/// <see cref="GeometryImage"/> rather than by a <see cref="DrawingImage"/>, because Avalonia's drawing
/// images do not survive being painted once per row.
/// </para>
/// <para>
/// The box is carried here rather than read from <see cref="IImage.Size"/>, because the icons of a column
/// have to agree on a size even when their drawings do not: a wide logo and a small dot must line up.
/// </para>
/// </remarks>
public sealed class CompletionIcon
{
    /// <summary>Creates an icon drawn into a square box.</summary>
    /// <param name="image">The picture.</param>
    /// <param name="size">The side of the box, in layout units.</param>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is zero or negative.</exception>
    public CompletionIcon(IImage image, double size)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        Image = image;
        Size = size;
    }

    /// <summary>The picture.</summary>
    public IImage Image { get; }

    /// <summary>The side of the square the picture is drawn into, in layout units.</summary>
    public double Size { get; }

    /// <summary>Builds a vector icon from a path.</summary>
    /// <param name="path">An SVG-style path, drawn in a box that starts at the origin.</param>
    /// <param name="brush">What the shape is filled with.</param>
    /// <param name="size">The side of the box, in layout units.</param>
    /// <returns>The icon.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="brush"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is zero or negative.</exception>
    public static CompletionIcon FromPath(string path, IBrush brush, double size)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(brush);

        return new CompletionIcon(new GeometryImage(StreamGeometry.Parse(path), fill: brush), size);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Size} x {Size} icon";
}
