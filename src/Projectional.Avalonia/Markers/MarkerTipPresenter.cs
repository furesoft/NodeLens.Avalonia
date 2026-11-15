using Avalonia;
using Avalonia.Media;

using Projectional.Language;

namespace Projectional.Avalonia;

/// <summary>Measures and paints the box that describes the marker under the pointer.</summary>
/// <remarks>
/// <para>
/// A painter rather than a control or a popup, for the same reasons as
/// <see cref="CompletionPresenter"/>: the editor draws one surface and already knows where the markers
/// are, so the tip is drawn onto that surface with no second control to keep in step and nothing to
/// detach when the document closes. The cost is that the tip is clipped by the editor's own bounds -
/// which is what an embedded component wants, since it must never paint outside the space its host gave
/// it.
/// </para>
/// <para>
/// The text is wrapped and the box placed by <see cref="Editing.MarkerTip"/>, in the editing layer, so
/// this type only turns a finished list of lines into pixels. That is why <see cref="Measure"/> takes
/// lines rather than findings: the break has already been decided, and deciding it twice is how a box
/// ends up sized for one break and drawn with another.
/// </para>
/// </remarks>
public sealed class MarkerTipPresenter
{
    /// <summary>The blank between the border and the text.</summary>
    public const double Inset = 6;

    /// <summary>The widest the text is allowed to become before it is wrapped, in layout units.</summary>
    /// <remarks>
    /// A cap rather than a target: a one-word message still produces a narrow box, and a long explanation
    /// stops growing before it covers the document it is describing.
    /// </remarks>
    public const double MaximumTextWidth = 400;

    private AvaloniaTextMeasure _measure;

    private MarkerTipPalette _palette;

    /// <summary>Creates a painter that draws through <paramref name="measure"/>.</summary>
    /// <param name="measure">The measure the editor lays out with, so the tip's text is shaped like the document's.</param>
    /// <param name="palette">What the box looks like.</param>
    /// <exception cref="ArgumentNullException"><paramref name="measure"/> or <paramref name="palette"/> is <see langword="null"/>.</exception>
    public MarkerTipPresenter(AvaloniaTextMeasure measure, MarkerTipPalette palette)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(palette);

        _measure = measure;
        _palette = palette;
    }

    /// <summary>The measure the text is shaped with.</summary>
    /// <remarks>
    /// The editor hands over the measure it lays out with, so the tip follows a font-size change instead
    /// of keeping the height it was first sized at.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public AvaloniaTextMeasure TextMeasure
    {
        get => _measure;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _measure = value;
        }
    }

    /// <summary>What the box looks like.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public MarkerTipPalette Palette
    {
        get => _palette;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _palette = value;
        }
    }

    /// <summary>The height of one line of text, which is what the box's height is counted in.</summary>
    public double LineHeight => Math.Round(_measure.LineHeight);

    /// <summary>How large the box is for <paramref name="lines"/>.</summary>
    /// <param name="lines">The lines to draw, as <see cref="Editing.MarkerTip.Lines"/> returned them.</param>
    /// <returns>The size, including the border and the inset. Empty when there is nothing to draw.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    public Size Measure(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return default;
        }

        double width = 0;

        foreach (var line in lines)
        {
            width = Math.Max(width, _measure.Measure(line, TextEmphasis.None));
        }

        return new Size(
            Math.Ceiling(width) + (2 * Inset),
            (lines.Count * LineHeight) + (2 * Inset));
    }

    /// <summary>Paints <paramref name="lines"/> into <paramref name="bounds"/>.</summary>
    /// <param name="context">The surface to paint on.</param>
    /// <param name="lines">The lines to draw, as <see cref="Editing.MarkerTip.Lines"/> returned them.</param>
    /// <param name="bounds">Where the box sits; use the size <see cref="Measure"/> asked for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="lines"/> is <see langword="null"/>.</exception>
    public void Render(DrawingContext context, IReadOnlyList<string> lines, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return;
        }

        context.DrawRectangle(_palette.Background, new Pen(_palette.Border, 1), bounds, 2, 2);

        var left = bounds.X + Inset;
        var top = bounds.Y + Inset;
        var lineHeight = LineHeight;

        foreach (var line in lines)
        {
            // A blank line is a paragraph break: it takes its height and draws nothing.
            if (line.Length > 0)
            {
                var text = _measure.Shaped(line, _palette.Foreground, TextEmphasis.None);

                context.DrawText(text, new Point(left, top));
            }

            top += lineHeight;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{_palette} at {LineHeight} per line";
}
