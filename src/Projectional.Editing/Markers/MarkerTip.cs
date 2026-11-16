using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>Decides what a marker's tooltip says and where it goes.</summary>
/// <remarks>
/// <para>
/// Two questions, both answerable without a toolkit: how the text has to be broken so it fits the room it
/// is given, and which corner of the page the box belongs in. The frontend measures glyphs and fills the
/// rectangle; nothing here knows what a font or a window is.
/// </para>
/// <para>
/// The text is laid out here rather than left to the drawing code, because a box cannot be placed before
/// its height is known and a box that runs off the bottom of the page has to be flipped above the marker.
/// Wrapping is therefore a layout decision, not a trimming decision.
/// </para>
/// </remarks>
public static class MarkerTip
{
    /// <summary>How far the box clears the marker it belongs to, in layout units.</summary>
    public const double Gap = 2;

    /// <summary>What the box says, one drawn line per entry.</summary>
    /// <param name="diagnostics">The findings to describe, in the order they should be read.</param>
    /// <param name="maxWidth">The widest a line may be, in layout units.</param>
    /// <param name="measure">What to measure a candidate line with.</param>
    /// <returns>
    /// The lines to draw, with an empty string between two findings. Empty when there is nothing to say.
    /// </returns>
    /// <param name="fixCountOf">
    /// How many repairs a finding offers, or <see langword="null"/> to say nothing about repairs. A count
    /// above zero adds the line that closes an open promise from [markers.md](markers.md): the tip used to
    /// be silent about fixes because there were none to promise, and now that there are, staying silent
    /// would hide the one thing the tooltip can tell the user that they cannot see.
    /// </param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWidth"/> is not positive.</exception>
    public static IReadOnlyList<string> Lines(
        IReadOnlyList<Diagnostic> diagnostics,
        double maxWidth,
        ITextMeasure measure,
        Func<Diagnostic, int>? fixCountOf = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);

        var lines = new List<string>();

        foreach (var diagnostic in diagnostics)
        {
            var paragraph = TipText.Wrap(Heading(diagnostic), maxWidth, measure);

            if (paragraph.Count == 0)
            {
                continue;
            }

            // One blank line between two findings and none around the block, so a single finding has no
            // empty line above it and the box has no padding to explain.
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(paragraph);

            var fixes = fixCountOf?.Invoke(diagnostic) ?? 0;

            if (fixes > 0)
            {
                lines.AddRange(TipText.Wrap(Offer(fixes), maxWidth, measure));
            }
        }

        return lines;
    }

    /// <summary>How the tip offers the repairs of one finding.</summary>
    /// <param name="count">How many repairs the finding has, which is always at least one here.</param>
    /// <returns>The sentence, naming the gesture that shows them.</returns>
    private static string Offer(int count) => count == 1
        ? "1 quick fix - Alt+Enter"
        : $"{count} quick fixes - Alt+Enter";

    /// <summary>Where the box goes so that it stays on the page.</summary>
    /// <param name="anchor">The marker the box describes, in layout units.</param>
    /// <param name="size">How big the box is.</param>
    /// <param name="viewport">How much room there is, in the same units.</param>
    /// <returns>The box, entirely inside the viewport whenever the viewport is at least as big as the box.</returns>
    /// <remarks>
    /// Below the marker by preference, so the box never covers the squiggle it is explaining; above it when
    /// there is no room below, which is the last line of a scrolled-to-the-bottom page. A box bigger than
    /// the page is pinned to the top-left rather than made to hang off it.
    /// </remarks>
    public static LayoutRect Place(LayoutRect anchor, LayoutSize size, LayoutSize viewport)
    {
        var y = anchor.Y + anchor.Height + Gap;

        if (y + size.Height > viewport.Height)
        {
            y = anchor.Y - Gap - size.Height;
        }

        return new LayoutRect(
            Math.Clamp(anchor.X, 0, Math.Max(0, viewport.Width - size.Width)),
            Math.Clamp(y, 0, Math.Max(0, viewport.Height - size.Height)),
            size.Width,
            size.Height);
    }

    /// <summary>What one finding reads as: how much it matters, what it says, and which check said it.</summary>
    /// <param name="diagnostic">The finding.</param>
    /// <returns>One sentence, never empty unless the message itself is.</returns>
    private static string Heading(Diagnostic diagnostic) => diagnostic.Code is { Length: > 0 } code
        ? $"{diagnostic.Severity}: {diagnostic.Message} ({code})"
        : $"{diagnostic.Severity}: {diagnostic.Message}";

}
