using Avalonia;
using Avalonia.Media;

using Projectional.Semantics;

namespace Projectional.Avalonia;

/// <summary>
/// Draws the squiggle under a marked stretch of text.
/// </summary>
/// <remarks>
/// <para>
/// A wave rather than a line, because a straight rule under a word is indistinguishable from an underline
/// the notation asked for - and <see cref="Projectional.Language.TextEmphasis.Underline"/> exists, so the
/// two would collide. A wave is also what every other code editor settled on, so it needs no explaining.
/// </para>
/// <para>
/// The pattern is a function of severity: a solid wave for the two that mean "something is wrong", a
/// dotted one for the two that mean "worth a look". That is one more signal than colour alone, which
/// matters for a reader who cannot separate red from amber.
/// </para>
/// <para>
/// The wave lives inside the line the layout reserved - the bottom of it, never below - so a marker can
/// never overlap the line underneath, and its amplitude shrinks if the font leaves no room. Nothing here
/// measures text: the rectangle it is handed is the whole input.
/// </para>
/// </remarks>
internal static class MarkerWave
{
    /// <summary>How wide one full up-and-down of the wave is, in layout units.</summary>
    internal const double Period = 4;

    /// <summary>How far the wave rises from the baseline, in layout units.</summary>
    internal const double Amplitude = 2;

    /// <summary>How thick the stroke is, in layout units.</summary>
    internal const double Thickness = 1;

    /// <summary>Whether a severity is drawn as a dotted wave rather than a solid one.</summary>
    /// <param name="severity">How much a finding matters.</param>
    /// <returns><see langword="true"/> for the two mild severities.</returns>
    internal static bool IsDotted(DiagnosticSeverity severity) =>
        severity is DiagnosticSeverity.Info or DiagnosticSeverity.Hint;

    /// <summary>Strokes one run.</summary>
    /// <param name="context">What to draw into.</param>
    /// <param name="brush">The colour of the run.</param>
    /// <param name="severity">The severity of the run, which picks the pattern.</param>
    /// <param name="bounds">The stretch of line to stroke, in control coordinates.</param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    internal static void Draw(DrawingContext context, IBrush brush, DiagnosticSeverity severity, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(brush);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // The dash is two thicknesses long, not one: a one-unit dash on a one-unit-thick stroke falls
        // between pixel centres and antialiases to roughly half the brush's opacity, which would make the
        // mild severities look fainter than the serious ones - the opposite of what the pattern is for.
        var pen = new Pen(brush, Thickness, IsDotted(severity) ? new DashStyle([Thickness * 2, Thickness * 2], 0) : null);

        context.DrawGeometry(null, pen, Path(bounds));
    }

    /// <summary>The wave of one run as a single figure.</summary>
    /// <param name="bounds">The stretch of line to stroke.</param>
    /// <returns>One open figure, one segment per half period.</returns>
    private static StreamGeometry Path(Rect bounds)
    {
        var baseline = bounds.Bottom - (Thickness / 2);
        var amplitude = Math.Min(Amplitude, Math.Max(0, bounds.Height - Thickness));
        var crest = baseline - amplitude;
        var step = Period / 2;
        var right = bounds.Right;

        var geometry = new StreamGeometry();

        using var figure = geometry.Open();

        var x = bounds.X;
        var y = baseline;

        figure.BeginFigure(new Point(x, y), isFilled: false);

        while (x + step < right)
        {
            x += step;
            y = y == baseline ? crest : baseline;
            figure.LineTo(new Point(x, y));
        }

        if (x < right)
        {
            figure.LineTo(new Point(right, y == baseline ? crest : baseline));
        }

        figure.EndFigure(isClosed: false);

        return geometry;
    }
}
