namespace Projectional.Editing;

/// <summary>
/// Turns the per-finding lines of a <see cref="MarkerSet"/> into the stretches that are actually stroked.
/// </summary>
/// <remarks>
/// <para>
/// The rule is a sweep per line: walk the markers of one line left to right and merge the ones that
/// overlap, keeping the worse severity. Markers that merely touch are <em>not</em> merged - a warning that
/// ends where an error begins has to stay a warning, so the two are drawn as two runs that happen to meet.
/// </para>
/// <para>
/// This lives in the editing layer, next to the geometry, rather than in the control, for the same reason
/// as everything else here: "which stretch of text is stroked, and in what colour" is a decision about the
/// document, and it is worth a test that needs no window.
/// </para>
/// </remarks>
public static class MarkerRuns
{
    /// <summary>The runs to stroke for <paramref name="markers"/>, in reading order.</summary>
    /// <param name="markers">The per-line markers of a document.</param>
    /// <returns>
    /// One run per stretch of text that is marked, top to bottom and left to right. Empty when nothing is
    /// marked.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="markers"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<MarkerRun> Of(IEnumerable<MarkerLine> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);

        var ordered = markers.ToArray();

        if (ordered.Length == 0)
        {
            return [];
        }

        // Reading order first, so the sweep below only ever has to compare neighbours on one line and the
        // answer comes back already sorted for the renderer.
        Array.Sort(ordered, static (left, right) =>
        {
            var byLine = left.Y.CompareTo(right.Y);

            return byLine != 0 ? byLine : left.X.CompareTo(right.X);
        });

        var runs = new List<MarkerRun>(ordered.Length);

        var index = 0;

        while (index < ordered.Length)
        {
            var line = ordered[index].Y;
            var left = ordered[index].X;
            var right = ordered[index].X + ordered[index].Width;
            var height = ordered[index].Height;
            var severity = ordered[index].Diagnostic.Severity;

            index++;

            while (index < ordered.Length && ordered[index].Y == line)
            {
                var marker = ordered[index];
                var start = marker.X;
                var end = marker.X + marker.Width;

                // Strictly less, not less-or-equal: two runs that touch are two findings, and merging them
                // would paint the milder one in the worse one's colour.
                if (start < right)
                {
                    right = Math.Max(right, end);
                    height = Math.Max(height, marker.Height);
                    severity = marker.Diagnostic.Severity > severity ? marker.Diagnostic.Severity : severity;
                }
                else
                {
                    runs.Add(new MarkerRun(left, line, right - left, height, severity));

                    left = start;
                    right = end;
                    height = marker.Height;
                    severity = marker.Diagnostic.Severity;
                }

                index++;
            }

            runs.Add(new MarkerRun(left, line, right - left, height, severity));
        }

        return runs;
    }
}
