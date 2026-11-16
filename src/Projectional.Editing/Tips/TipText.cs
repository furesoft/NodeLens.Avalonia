using System.Text;

using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Breaks the text of a tooltip so that no line is wider than the box it is drawn in.</summary>
/// <remarks>
/// <para>
/// One wrapper for every tip, because a box is placed before it is drawn: its height decides where it goes,
/// so wrapping is a layout decision rather than a trimming decision, and two wrappers would eventually
/// disagree about a width. The marker tip and the concept tip both come through here.
/// </para>
/// <para>
/// It measures rather than counts characters: a box that is wide enough for 40 narrow letters is not wide
/// enough for 40 wide ones, and the measure is the only thing that knows the difference.
/// </para>
/// </remarks>
internal static class TipText
{
    /// <summary>Breaks one paragraph so no line is wider than the room it is given.</summary>
    /// <param name="text">The paragraph to break. Runs of blanks collapse to one.</param>
    /// <param name="maxWidth">The widest a line may be, in layout units.</param>
    /// <param name="measure">What to measure a candidate line with.</param>
    /// <returns>The lines, in reading order. Empty when there is nothing to draw.</returns>
    /// <remarks>
    /// A word wider than the box is broken rather than allowed to overflow, because the box has to be
    /// placed before it is drawn and an overflowing line would make the width a lie.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Wrap(string text, double maxWidth, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);

        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0)
            {
                if (measure.Measure($"{line} {word}", TextEmphasis.None) <= maxWidth)
                {
                    line.Append(' ').Append(word);

                    continue;
                }

                lines.Add(line.ToString());
                line.Clear();
            }

            var rest = word;

            while (!Fits(rest, maxWidth, measure))
            {
                var take = Fitting(rest, maxWidth, measure);

                lines.Add(rest[..take]);
                rest = rest[take..];
            }

            line.Append(rest);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    /// <summary>Whether a run of text fits the room it is given.</summary>
    private static bool Fits(string text, double maxWidth, ITextMeasure measure) =>
        measure.Measure(text, TextEmphasis.None) <= maxWidth;

    /// <summary>How many characters of a word fit on one line.</summary>
    /// <returns>The length of the longest prefix that fits, and never zero.</returns>
    /// <remarks>
    /// One character is the floor: a box narrower than a single glyph would otherwise take nothing, add
    /// nothing, and never advance.
    /// </remarks>
    private static int Fitting(string text, double maxWidth, ITextMeasure measure)
    {
        var take = 1;

        while (take < text.Length && Fits(text[..(take + 1)], maxWidth, measure))
        {
            take++;
        }

        return take;
    }
}
