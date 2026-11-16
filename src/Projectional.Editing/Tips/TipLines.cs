using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>The lines a tooltip draws: the heading first, then the description broken to fit the room.</summary>
/// <remarks>
/// One wrapper for both tips, because a box is placed before it is drawn - its height decides where it goes, so
/// wrapping is a layout decision rather than a trimming decision. The heading is broken like any other text, so a
/// name longer than the box cannot make the measured width a lie; in practice it stays one line, because a
/// concept key and a slot name are names and not sentences.
/// </remarks>
public static class TipLines
{
    /// <summary>The lines to draw for <paramref name="content"/>.</summary>
    /// <param name="content">What the tip says.</param>
    /// <param name="maxWidth">The widest a line may be, in layout units.</param>
    /// <param name="measure">What to measure a candidate line with.</param>
    /// <returns>The lines in reading order, never empty for a real content.</returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWidth"/> is not positive.</exception>
    public static IReadOnlyList<string> Of(TipContent content, double maxWidth, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);

        var lines = new List<string>(TipText.Wrap(content.Heading, maxWidth, measure));

        lines.AddRange(TipText.Wrap(content.Description, maxWidth, measure));

        return lines;
    }
}
