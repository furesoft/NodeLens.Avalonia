using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>Answers which finding the pointer is on.</summary>
/// <remarks>
/// <para>
/// A marker is stroked as a wave in the bottom strip of the line the layout reserved, so the hit test only
/// looks at that strip. Pointing at the word itself must not open a tooltip: the message would cover the
/// text the reader is looking at, and the word is what the caret and the selection are for.
/// </para>
/// <para>
/// This runs against <see cref="MarkerLine"/> rather than against <see cref="MarkerRun"/>, because a run is
/// what the page paints and has already thrown the <see cref="Diagnostic"/> away - a hit test built on runs
/// could say "something is here" but never what.
/// </para>
/// <para>
/// Coordinates are layout units, the same ones <see cref="MarkerGeometry"/> produced. Converting from
/// control pixels (a scroll offset, a gutter) is the frontend's business, not this type's.
/// </para>
/// </remarks>
public static class MarkerHitTest
{
    /// <summary>The findings under a point, or <see langword="null"/> when it is on no marker.</summary>
    /// <param name="markers">The markers of the document, in reading order.</param>
    /// <param name="x">The horizontal position to test, in layout units.</param>
    /// <param name="y">The vertical position to test, in layout units.</param>
    /// <param name="bandHeight">
    /// How tall the strip above the bottom of the line is, in layout units. A marker whose line is shorter
    /// than this is reachable over its whole height.
    /// </param>
    /// <returns>
    /// Every finding on the marker under the point, the worst first and each one once, together with the
    /// marker it was found on; or <see langword="null"/> when the point is on none of them.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="markers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bandHeight"/> is negative.</exception>
    public static MarkerHit? At(IReadOnlyList<MarkerLine> markers, double x, double y, double bandHeight)
    {
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentOutOfRangeException.ThrowIfNegative(bandHeight);

        MarkerLine anchor = default;
        var found = new List<Diagnostic>();

        foreach (var marker in markers)
        {
            if (!InBand(marker, x, y, bandHeight))
            {
                continue;
            }

            if (found.Count == 0)
            {
                anchor = marker;
            }

            // One finding can cover several lines - a long value, a whole slot - so the same diagnostic
            // arrives once per line. By reference rather than by value: two checks reporting the same
            // sentence about the same node are two findings, not one.
            if (!found.Exists(diagnostic => ReferenceEquals(diagnostic, marker.Diagnostic)))
            {
                found.Add(marker.Diagnostic);
            }
        }

        return found.Count == 0 ? null : new MarkerHit([.. found.OrderByDescending(diagnostic => diagnostic.Severity)], anchor);
    }

    /// <summary>Whether a point is inside the strip of the line the wave is stroked in.</summary>
    /// <param name="marker">The marker to test.</param>
    /// <param name="x">The horizontal position to test.</param>
    /// <param name="y">The vertical position to test.</param>
    /// <param name="bandHeight">How tall the strip is.</param>
    /// <returns><see langword="true"/> when the point is on the wave.</returns>
    private static bool InBand(MarkerLine marker, double x, double y, double bandHeight)
    {
        var right = marker.X + marker.Width;
        var bottom = marker.Y + marker.Height;
        var top = Math.Max(marker.Y, bottom - bandHeight);

        return x >= marker.X && x < right && y >= top && y < bottom;
    }
}
