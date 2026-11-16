using Avalonia;
using Avalonia.Media;

using Projectional.Editing;
using Projectional.Language;

namespace Projectional.Avalonia;

/// <summary>
/// Measures and paints an open <see cref="QuickFixList"/> as a column of repair labels.
/// </summary>
/// <remarks>
/// <para>
/// A painter rather than a control, for the reasons <see cref="CompletionPresenter"/> gives: the editor
/// draws one surface and already knows where the caret is, so there is no second control to keep in step
/// and nothing to detach when the document closes. The two presenters are deliberately separate types -
/// a completion row is an icon and a concept name, a repair row is a sentence - but they share one
/// <see cref="CompletionPalette"/>, because a list drawn on the document is one kind of surface and a
/// host should not have to keep two boxes looking alike by hand.
/// </para>
/// <para>
/// This type owns no editing rule. It reports the size it needs and which row a point hit; opening the
/// list, moving the highlight and accepting an entry all happen in the editing layer.
/// </para>
/// </remarks>
public sealed class QuickFixPresenter
{
    /// <summary>The blank between the border and the first row.</summary>
    private const double Inset = 4;

    /// <summary>The blank above and below a row's content, which is what makes a row easy to hit.</summary>
    private const double RowPadding = 2;

    /// <summary>The narrowest the list is drawn, so a short label does not produce a stamp-sized popup.</summary>
    private const double MinimumWidth = 120;

    private AvaloniaTextMeasure _measure;

    private CompletionPalette _palette;

    /// <summary>Creates a painter that draws through <paramref name="measure"/>.</summary>
    /// <param name="measure">The measure the editor lays out with, so a row is as tall as a line of text.</param>
    /// <param name="palette">What the box, its rows and its highlight look like.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="measure"/> or <paramref name="palette"/> is <see langword="null"/>.
    /// </exception>
    public QuickFixPresenter(AvaloniaTextMeasure measure, CompletionPalette palette)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(palette);

        _measure = measure;
        _palette = palette;
    }

    /// <summary>How many rows are shown at once before the window follows the highlight.</summary>
    /// <remarks>
    /// Fewer than the completion list shows, because the list is short by nature: it holds the repairs of
    /// the findings on one line, not the concepts a slot accepts. The window still exists so that a line
    /// with many findings cannot grow a box taller than the control.
    /// </remarks>
    public int VisibleRows { get; set; } = 8;

    /// <summary>The measure labels are shaped with.</summary>
    /// <remarks>
    /// A host that changes the font size has to set this too, or the rows keep the old height and stop
    /// lining up with the text they describe.
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

    /// <summary>What the box, its rows and its highlight look like.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public CompletionPalette Palette
    {
        get => _palette;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _palette = value;
        }
    }

    /// <summary>The height of one row, which the editor needs to place the list beside the caret.</summary>
    public double RowHeight => Math.Round(_measure.LineHeight) + (2 * RowPadding);

    /// <summary>How large the list is drawn for <paramref name="list"/>.</summary>
    /// <param name="list">The list on show.</param>
    /// <returns>The size, including the border and the inset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is <see langword="null"/>.</exception>
    public Size Measure(QuickFixList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        var labelWidth = 0.0;

        foreach (var fix in list.Fixes)
        {
            labelWidth = Math.Max(labelWidth, _measure.Measure(fix.Label, TextEmphasis.None));
        }

        return new Size(
            (2 * Inset) + Math.Max(MinimumWidth, labelWidth),
            (2 * Inset) + (Rows(list) * RowHeight));
    }

    /// <summary>Paints <paramref name="list"/> into <paramref name="bounds"/>.</summary>
    /// <param name="context">The surface to paint on.</param>
    /// <param name="list">The list on show.</param>
    /// <param name="bounds">Where the list sits; use the size <see cref="Measure"/> asked for.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="list"/> is <see langword="null"/>.
    /// </exception>
    public void Render(DrawingContext context, QuickFixList list, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(list);

        context.DrawRectangle(_palette.Background, new Pen(_palette.Border, 1), bounds, 2, 2);

        var rows = Rows(list);
        var first = FirstVisible(list, rows);
        var rowHeight = RowHeight;
        var labelLeft = bounds.X + Inset;

        for (var row = 0; row < rows; row++)
        {
            var index = first + row;
            var fix = list[index];
            var top = bounds.Y + Inset + (row * rowHeight);

            if (index == list.Selected)
            {
                context.FillRectangle(
                    _palette.SelectionBackground,
                    new Rect(bounds.X + 1, top, bounds.Width - 2, rowHeight));
            }

            var label = _measure.Shaped(fix.Label, ForegroundOf(fix, index == list.Selected), TextEmphasis.None);

            context.DrawText(label, new Point(labelLeft, top + ((rowHeight - label.Height) / 2)));
        }
    }

    /// <summary>Which row of <paramref name="list"/> is at <paramref name="point"/>, if any.</summary>
    /// <param name="list">The list on show.</param>
    /// <param name="bounds">Where the list was painted.</param>
    /// <param name="point">The point, in the same coordinates as <paramref name="bounds"/>.</param>
    /// <returns>The index into <see cref="QuickFixList.Fixes"/>, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The index rather than the repair, so the caller can turn "the second row was clicked" into the two
    /// actions the keyboard runs: select it, then accept it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is <see langword="null"/>.</exception>
    public int? RowAt(QuickFixList list, Rect bounds, Point point)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (!bounds.Contains(point))
        {
            return null;
        }

        var row = (int)Math.Floor((point.Y - bounds.Y - Inset) / RowHeight);

        if (row < 0 || row >= Rows(list))
        {
            return null;
        }

        var index = FirstVisible(list, Rows(list)) + row;

        return index < list.Count ? index : null;
    }

    /// <inheritdoc />
    public override string ToString() => $"{_palette} at {RowHeight} per row";

    /// <summary>The colour of one row's label.</summary>
    /// <remarks>
    /// A repair that cannot run is dimmed whether or not it is highlighted: the highlight says "this is the
    /// row a press would take", the dimming says "and it would refuse". The palette's line colour is the
    /// dim one, because a list palette has no unavailable state of its own and inventing a second palette
    /// for one brush would make a host keep two boxes looking alike by hand.
    /// </remarks>
    private IBrush ForegroundOf(DiagnosticFix fix, bool selected) =>
        fix.IsDisabled ? _palette.Border : selected ? _palette.SelectionForeground : _palette.Foreground;

    private int Rows(QuickFixList list) => Math.Min(list.Count, Math.Max(1, VisibleRows));

    private static int FirstVisible(QuickFixList list, int rows) =>
        Math.Clamp(list.Selected - (rows / 2), 0, list.Count - rows);
}
