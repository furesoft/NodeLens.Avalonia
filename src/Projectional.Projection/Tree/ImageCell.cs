namespace Projectional.Projection;

/// <summary>
/// A picture on the line, drawn by the host from the name the notation gave it.
/// </summary>
/// <remarks>
/// <para>
/// The cell carries the key and the box and no pixels: what a unit is belongs to the measure, and which picture
/// a key names belongs to the host. That division is what lets the layout size a picture with no bitmap in
/// sight, and it is why <see cref="CellText.Of"/> answers nothing for this cell - a picture is not text, so a
/// renderer that only draws text draws its room and leaves it blank rather than inventing a glyph.
/// </para>
/// <para>
/// <see cref="EditorCell.Paint"/> applies as it does to any cell, so a language can frame its pictures; the
/// image is drawn inside that decoration, over its fill.
/// </para>
/// </remarks>
public sealed class ImageCell : EditorCell
{
    public ImageCell(CellAddress address, string key, double width, double height)
        : base(address)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Key = key;
        Width = width;
        Height = height;
    }

    /// <summary>The name of the picture, which the frame hands to the host.</summary>
    public string Key { get; }

    /// <summary>The box's width in layout units, or zero for the line's height.</summary>
    public double Width { get; }

    /// <summary>The box's height in layout units, or zero for the line's height.</summary>
    public double Height { get; }

    internal override string Describe() => $"image {Key}";
}
