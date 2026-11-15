using Projectional.Language;

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
    /// <param name="text">The text to measure.</param>
    /// <param name="emphasis">
    /// How the text is decorated. It is part of the measurement rather than of the painting, because a
    /// bold or italic face is wider than the regular one: a measure that ignored it would hand the
    /// layout a box sized for a face that is not the one drawn in it, and the caret, the hit test and
    /// the selection - all of which are computed from those boxes - would drift.
    /// </param>
    double Measure(string text, TextEmphasis emphasis);

    /// <summary>The height of one line, which is also the vertical step between two lines.</summary>
    double LineHeight { get; }

    /// <summary>How far one indentation level pushes content to the right.</summary>
    double IndentWidth { get; }
}
