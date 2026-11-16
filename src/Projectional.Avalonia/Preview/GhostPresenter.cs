using Avalonia;
using Avalonia.Media;

using Projectional.Editing;
using Projectional.Projection;

namespace Projectional.Avalonia;

/// <summary>
/// Paints a preview's ghost: the copy's text, a tint behind every cell the edit touched, and the badge.
/// </summary>
/// <remarks>
/// <para>
/// The ghost replaces the page rather than covering it, so this is a whole-surface painter and not an overlay: the
/// background is painted first, then the tints, then the text. It knows nothing about intentions, timers or
/// hover - a <see cref="GhostFrame"/> is already the answer to "what should be on screen", and this turns it into
/// pixels, exactly as the control does for an <see cref="EditorFrame"/>.
/// </para>
/// <para>
/// <b>Nothing here decides a colour</b>: every brush comes from <see cref="PreviewPalette"/> and the text palette
/// of the theme, so a host that themes the page themes the preview too.
/// </para>
/// </remarks>
public sealed class GhostPresenter
{
    /// <summary>The gap between the badge and the edge of the surface.</summary>
    public const double BadgeInset = 8;

    private readonly AvaloniaTextMeasure _measure;
    private readonly MarkerTipPresenter _badge;

    /// <summary>Paints ghosts with <paramref name="measure"/> and the theme's tip palette for the badge.</summary>
    /// <param name="measure">The measure the ghost's geometry was computed with.</param>
    /// <param name="palette">The colours the preview is drawn in.</param>
    /// <param name="tipPalette">The colours of the badge box.</param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public GhostPresenter(
        AvaloniaTextMeasure measure,
        PreviewPalette palette,
        MarkerTipPalette tipPalette)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(tipPalette);

        _measure = measure;
        _badge = new MarkerTipPresenter(measure, tipPalette);
        Palette = palette;
    }

    /// <summary>The colours the preview is drawn in.</summary>
    public PreviewPalette Palette { get; }

    /// <summary>Paints <paramref name="ghost"/> over the whole surface.</summary>
    /// <param name="context">What to draw into.</param>
    /// <param name="ghost">The frame to draw.</param>
    /// <param name="background">The page colour, painted first.</param>
    /// <param name="palette">The text colours, so the copy reads like the document it replaces.</param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <remarks>
    /// The badge goes last, in the top-right corner, so it is never buried by a run that happens to end under it.
    /// </remarks>
    public void Render(
        DrawingContext context,
        GhostFrame ghost,
        Rect surface,
        double gutter,
        IBrush background,
        SyntaxPalette palette)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ghost);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(palette);

        context.FillRectangle(background, surface);

        // The frame's runs are in layout coordinates, and the viewport plus the margin is what turns them into
        // something a DrawingContext can use - the same conversion the page's own runs go through.
        foreach (var run in ghost.TextRuns)
        {
            if (ghost.ChangeOf(run) is { } change)
            {
                context.FillRectangle(Palette.BrushOf(change.Kind), Box(ghost.Viewport.ToViewport(run.Bounds), gutter));
            }
        }

        foreach (var run in ghost.TextRuns)
        {
            var bounds = Box(ghost.Viewport.ToViewport(run.Bounds), gutter);
            var brush = ghost.IsDimmed(run) ? Palette.Dimmed : palette.BrushOf(run.Style);

            context.DrawText(
                _measure.Shaped(run.Text, brush, run.Emphasis),
                new Point(bounds.X, bounds.Y));
        }

        RenderBadge(context, ghost.Badge, surface);
    }

    /// <summary>Paints the badge in the top-right corner of the surface.</summary>
    /// <param name="context">What to draw into.</param>
    /// <param name="badge">The sentence to show.</param>
    /// <param name="surface">The area the ghost was painted into, in control pixels.</param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public void RenderBadge(DrawingContext context, string badge, Rect surface)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(badge);

        var lines = new[] { badge };
        var size = _badge.Measure(lines);

        var box = new Rect(
            Math.Max(0, surface.Right - size.Width - BadgeInset),
            BadgeInset,
            Math.Min(size.Width, Math.Max(0, surface.Width)),
            size.Height);

        _badge.Render(context, lines, box);
    }

    public override string ToString() => $"ghost in {Palette}";

    private static Rect Box(LayoutRect rect, double gutter) =>
        new(rect.X + gutter, rect.Y, rect.Width, rect.Height);
}
