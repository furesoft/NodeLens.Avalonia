namespace Projectional.Projection;

/// <summary>
/// Turns text into a width, and says how tall a line is.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam where layout stops being independent of fonts. Everything else in the projection
/// layer speaks in layout units; only a measure knows what a unit is. That is what lets the same
/// arrangement code drive a snapshot test with no window and a control with real glyphs, and it is
/// why arrangement never measures a string itself.
/// </para>
/// <para>
/// A measure is expected to be deterministic for the same input, because layout caches results and
/// tests compare them.
/// </para>
/// </remarks>
public interface ITextMeasure
{
    /// <summary>The width of <paramref name="text"/> in layout units.</summary>
    double Measure(string text);

    /// <summary>The height of one line, which is also the vertical step between two lines.</summary>
    double LineHeight { get; }

    /// <summary>How far one indentation level pushes content to the right.</summary>
    double IndentWidth { get; }
}
