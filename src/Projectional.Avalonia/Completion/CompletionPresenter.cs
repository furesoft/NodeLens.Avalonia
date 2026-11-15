using Avalonia;
using Avalonia.Media;

using Projectional.Editing;
using Projectional.Language;

namespace Projectional.Avalonia;

/// <summary>
/// Measures and paints an open <see cref="CompletionList"/> as a column of icons and labels.
/// </summary>
/// <remarks>
/// <para>
/// A painter rather than a control. The editor draws one surface and already knows where the caret is,
/// so the list is drawn onto that surface at a position the editor computes - there is no second control
/// to keep in step, no popup to place, and nothing to detach when the document closes. The cost is that
/// the list cannot extend past the editor's own bounds, which is what an embedded component wants
/// anyway: it must not paint outside the space its host gave it.
/// </para>
/// <para>
/// This type owns no editing rule. It reports the size it needs and which row a point hit; opening,
/// filtering, moving the highlight and filling the slot all happen in the editing layer, driven from
/// the control.
/// </para>
/// </remarks>
public sealed class CompletionPresenter
{
    /// <summary>The blank between the border and the first row.</summary>
    private const double Inset = 4;

    /// <summary>The blank above and below a row's content, which is what makes a row easy to hit.</summary>
    private const double RowPadding = 2;

    /// <summary>The gap between a row's icon and its label.</summary>
    private const double IconGap = 6;

    /// <summary>The narrowest the list is drawn, so a short label does not produce a stamp-sized popup.</summary>
    private const double MinimumWidth = 120;

    private AvaloniaTextMeasure _measure;

    private CompletionPalette _palette;

    /// <summary>Creates a painter that draws through <paramref name="measure"/>.</summary>
    /// <param name="measure">The measure the editor lays out with, so a label is shaped the same way twice.</param>
    /// <param name="palette">What the list and its icons look like.</param>
    /// <exception cref="ArgumentNullException"><paramref name="measure"/> or <paramref name="palette"/> is <see langword="null"/>.</exception>
    public CompletionPresenter(AvaloniaTextMeasure measure, CompletionPalette palette)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(palette);

        _measure = measure;
        _palette = palette;
    }

    /// <summary>How many rows are shown at once before the window follows the highlight.</summary>
    /// <remarks>
    /// A window rather than a scroll bar, because the list is driven from the keyboard: the highlighted
    /// row is always visible and moving the highlight moves the window. A list longer than this is
    /// unusual - the proposals are the concepts a slot accepts, not a vocabulary.
    /// </remarks>
    public int VisibleRows { get; set; } = 12;

    /// <summary>The measure labels are shaped with.</summary>
    /// <remarks>
    /// The editor hands over the measure it lays out with, so a row is as tall as a line of text and a
    /// label is as wide as the cell it names. A host that changes the font size has to set this too,
    /// or the list keeps the old row height and the rows stop lining up with the text.
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

    /// <summary>What the list and its icons look like.</summary>
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
    public Size Measure(CompletionList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (list.IsEmpty)
        {
            return default;
        }

        var (iconWidth, labelWidth) = ColumnWidths(list);

        return new Size(
            Inset + iconWidth + IconGap + labelWidth + Inset,
            (2 * Inset) + (Rows(list) * RowHeight));
    }

    /// <summary>Paints <paramref name="list"/> into <paramref name="bounds"/>.</summary>
    /// <param name="context">The surface to paint on.</param>
    /// <param name="list">The list on show.</param>
    /// <param name="bounds">Where the list sits; use the size <see cref="Measure"/> asked for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="list"/> is <see langword="null"/>.</exception>
    public void Render(DrawingContext context, CompletionList list, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(list);

        if (list.IsEmpty)
        {
            return;
        }

        context.DrawRectangle(_palette.Background, new Pen(_palette.Border, 1), bounds, 2, 2);

        var (iconWidth, _) = ColumnWidths(list);
        var rows = Rows(list);
        var first = FirstVisible(list, rows);
        var rowHeight = RowHeight;
        var labelLeft = bounds.X + Inset + iconWidth + IconGap;

        for (var row = 0; row < rows; row++)
        {
            var index = first + row;
            var item = list.Matches[index];
            var top = bounds.Y + Inset + (row * rowHeight);
            var selected = index == list.SelectedIndex;
            var foreground = selected ? _palette.SelectionForeground : _palette.Foreground;

            if (selected)
            {
                context.FillRectangle(
                    _palette.SelectionBackground,
                    new Rect(bounds.X + 1, top, bounds.Width - 2, rowHeight));
            }

            DrawIcon(context, _palette.IconOf(item.Kind), bounds.X + Inset, top, rowHeight, iconWidth);

            var label = _measure.Shaped(item.Label, foreground, TextEmphasis.None);

            context.DrawText(label, new Point(labelLeft, top + ((rowHeight - label.Height) / 2)));
        }
    }

    /// <summary>Which row of <paramref name="list"/> is at <paramref name="point"/>, if any.</summary>
    /// <param name="list">The list on show.</param>
    /// <param name="bounds">Where the list was painted.</param>
    /// <param name="point">The point, in the same coordinates as <paramref name="bounds"/>.</param>
    /// <returns>The index into <see cref="CompletionList.Matches"/>, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The index into the matches rather than the item, so the caller can turn "the third row was
    /// clicked" into the same two actions the keyboard runs: select it, then accept it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is <see langword="null"/>.</exception>
    public int? RowAt(CompletionList list, Rect bounds, Point point)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (list.IsEmpty || !bounds.Contains(point))
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

    /// <summary>Paints one icon centred in its row, scaled to the box the palette asked for.</summary>
    private static void DrawIcon(
        DrawingContext context,
        CompletionIcon icon,
        double left,
        double top,
        double rowHeight,
        double columnWidth)
    {
        // Never larger than the row, so a palette that asks for a bigger picture than the list has room
        // for shrinks instead of bleeding into its neighbours.
        var side = Math.Min(Math.Min(icon.Size, rowHeight), columnWidth);

        var x = left + ((columnWidth - side) / 2);
        var y = top + ((rowHeight - side) / 2);

        context.DrawImage(icon.Image, new Rect(x, y, side, side));
    }

    private int Rows(CompletionList list) => Math.Min(list.Count, Math.Max(1, VisibleRows));

    private static int FirstVisible(CompletionList list, int rows) =>
        Math.Clamp(list.SelectedIndex - (rows / 2), 0, list.Count - rows);

    private (double IconWidth, double LabelWidth) ColumnWidths(CompletionList list)
    {
        double iconWidth = 0;
        double labelWidth = 0;

        foreach (var item in list.Matches)
        {
            iconWidth = Math.Max(iconWidth, _palette.IconOf(item.Kind).Size);
            labelWidth = Math.Max(labelWidth, _measure.Measure(item.Label, TextEmphasis.None));
        }

        return (iconWidth, Math.Max(MinimumWidth, labelWidth));
    }
}
