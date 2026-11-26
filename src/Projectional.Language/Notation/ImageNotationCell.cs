namespace Projectional.Language;

/// <summary>
/// A picture on the line: an icon before a name, a logo beside a product. The notation says what the
/// picture <em>is</em>; the host owns the artwork.
/// </summary>
/// <remarks>
/// <para>
/// A picture is not text, so it cannot be a <see cref="ConstantNotationCell"/>: a glyph typed into a cell
/// depends on the font the line is shaped in - it can come out as a box, sit off the baseline, or change
/// width when the font falls back - whereas an image is drawn into a box the notation sized and looks the
/// same on every machine. That is the same reason the completion list carries <c>IImage</c>s rather than
/// characters.
/// </para>
/// <para>
/// <see cref="Key"/> is a name, never a path: a language that wrote <c>logo.png</c> would be naming a file on
/// the reader's disk, and the same language would stop drawing its icons the moment it was embedded in a host
/// that stores its artwork elsewhere. The host maps a key to a picture (a <c>Bitmap</c> decoded from a file, a
/// vector path, an image it already holds), exactly as a palette maps a <see cref="TextStyleKind"/> to a brush.
/// A key the host does not know is drawn as a placeholder box rather than as nothing, so a missing picture
/// reads as missing instead of as an empty line.
/// </para>
/// <para>
/// The box is declared by the notation in layout units - a symbol and a photograph need different room, and
/// neither can ask the layout what it wants to be. A side left at zero means "as tall as the line", which is
/// what an inline icon almost always is: it scales with the font, so it sits on the line it belongs to
/// instead of forcing the line to grow.
/// </para>
/// </remarks>
public sealed class ImageNotationCell : InlineNotationCell
{
    /// <summary>Creates a picture drawn into a box the notation sizes.</summary>
    /// <param name="key">The name of the picture, which the host resolves; never a path.</param>
    /// <param name="width">The box's width in layout units, or zero for the line's height.</param>
    /// <param name="height">The box's height in layout units, or zero for the line's height.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A side is negative.</exception>
    public ImageNotationCell(string key, double width = 0, double height = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Key = key;
        Width = width;
        Height = height;
    }

    /// <summary>The name of the picture, which the host resolves.</summary>
    public string Key { get; }

    /// <summary>The box's width in layout units, or zero for the line's height.</summary>
    public double Width { get; }

    /// <summary>The box's height in layout units, or zero for the line's height.</summary>
    public double Height { get; }

    public override string ToString() =>
        Width == 0 && Height == 0 ? $"image {Key}" : $"image {Key} ({Width} x {Height})";
}
