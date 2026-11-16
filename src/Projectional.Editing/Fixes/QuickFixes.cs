using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>Which repairs a document offers, asked from the caret or from a finding in hand.</summary>
/// <remarks>
/// <para>
/// Two questions, one answer shape, and both are headless: the popup, the bulb and <c>Alt+Enter</c> all
/// ask <see cref="At"/> about the line the caret is on, while a host that already holds a finding asks
/// <see cref="Of"/>. Nothing is cached - the answer is derived per call from the session's current marker
/// and fix sets - so nothing can be stale after an edit, an undo or a variant switch. One line's worth of
/// work over sets that are already indexed is what makes that affordable.
/// </para>
/// <para>
/// The line is resolved geometrically, because a document has no lines: only the projection does. The
/// caret's rectangle gives the top of the page line it is drawn on, and every marked stretch whose top is
/// that line is on it - which is what makes a marker on a wrapped continuation line count as "this line",
/// the line the user is looking at, instead of as a different one.
/// </para>
/// </remarks>
public static class QuickFixes
{
    /// <summary>The repairs offered for the line the caret is on, the worst finding's first.</summary>
    /// <param name="session">The document to ask.</param>
    /// <remarks>
    /// <para>
    /// The worst finding on the line leads, by <see cref="MarkerSet"/>'s own rule - document order, then
    /// severity descending - and its repairs come first; the rest follow in the set's order. Order is part
    /// of the visible behaviour, so it is fixed here rather than left to a dictionary.
    /// </para>
    /// <para>
    /// A finding whose check supplied no repairs contributes nothing, and does not lead the list either:
    /// the affordance exists because there is something to offer, so a line whose only finding has no fix
    /// offers none.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<DiagnosticFix> At(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (CaretBounds.Of(session.Tree, session.Layout, session.Selection.Caret, session.Measure)
            is not { } caret)
        {
            return [];
        }

        var findings = FindingsOnLine(session, caret.Y);

        if (findings.Count == 0)
        {
            return [];
        }

        var worst = 0;

        for (var index = 1; index < findings.Count; index++)
        {
            if (findings[index].Severity > findings[worst].Severity)
            {
                worst = index;
            }
        }

        var fixes = new List<DiagnosticFix>();

        fixes.AddRange(session.Fixes.For(findings[worst]));

        for (var index = 0; index < findings.Count; index++)
        {
            if (index != worst)
            {
                fixes.AddRange(session.Fixes.For(findings[index]));
            }
        }

        return fixes;
    }

    /// <summary>The repairs offered for <paramref name="diagnostic"/>, in the order the set holds them.</summary>
    /// <param name="session">The document to ask.</param>
    /// <param name="diagnostic">The finding to ask about.</param>
    /// <remarks>
    /// The finding is looked up by value, so a host that kept its own copy of a diagnostic gets the same
    /// answer as one that took it from the marker set. A finding nobody repaired answers empty.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/> or <paramref name="diagnostic"/> is <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<DiagnosticFix> Of(EditorSession session, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(diagnostic);

        return session.Fixes.For(diagnostic);
    }

    /// <summary>The findings on the caret's line that offer at least one repair, in marker-set order.</summary>
    /// <param name="session">The document to ask.</param>
    /// <param name="caretTop">The top of the line the caret is drawn on.</param>
    /// <remarks>
    /// A line is a band, not a coordinate: the comparison classifies by the nearest line top rather than
    /// by equality, so the last bit of a <see cref="double"/> - or a marker resolved from a different cell
    /// of the same line - cannot move a finding onto the line above.
    /// </remarks>
    private static List<Diagnostic> FindingsOnLine(EditorSession session, double caretTop)
    {
        var half = session.Measure.LineHeight / 2;
        var onLine = new HashSet<Diagnostic>();

        foreach (var line in MarkerGeometry.Of(session.Layout, session.Markers, session.Measure))
        {
            if (Math.Abs(line.Y - caretTop) < half)
            {
                onLine.Add(line.Diagnostic);
            }
        }

        var findings = new List<Diagnostic>();

        if (onLine.Count == 0)
        {
            return findings;
        }

        var seen = new HashSet<Diagnostic>();

        foreach (var diagnostic in session.Markers.Diagnostics)
        {
            if (onLine.Contains(diagnostic)
                && seen.Add(diagnostic)
                && session.Fixes.For(diagnostic).Count > 0)
            {
                findings.Add(diagnostic);
            }
        }

        return findings;
    }
}
