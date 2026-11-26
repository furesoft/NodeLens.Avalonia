using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Everything one painted frame of the control needs: the text to draw, what is highlighted, and where
/// the caret is.
/// </summary>
/// <remarks>
/// <para>
/// A frame is a value derived from a tree, a layout, a viewport and a selection, and it holds no
/// handles to windows. That is what keeps the interesting half of the control - what is visible, what
/// is highlighted, where the caret lands - testable without a window, with the same measure the layout
/// tests use. The control's job shrinks to converting these rectangles to screen coordinates and
/// painting them.
/// </para>
/// <para>
/// It is rebuilt rather than mutated. Editing a document re-projects it, which invalidates the layout,
/// which invalidates every rectangle in the frame; a frame that could be patched would need to know
/// which of its own cells changed. Building a new one costs a pass over the cells already on screen.
/// </para>
/// <para>
/// All rectangles are in layout coordinates. The viewport on the frame converts them, so the scroll
/// offset is subtracted in exactly one place.
/// </para>
/// </remarks>
public sealed class EditorFrame
{
    private EditorFrame(
        LayoutViewport viewport,
        ITextMeasure measure,
        IReadOnlyList<PlacedPaint> paints,
        IReadOnlyList<PlacedImage> images,
        IReadOnlyList<TextRun> textRuns,
        IReadOnlyList<LayoutRect> highlights,
        LayoutRect? caretRect,
        CaretPosition caret)
    {
        Viewport = viewport;
        Measure = measure;
        Paints = paints;
        Images = images;
        TextRuns = textRuns;
        Highlights = highlights;
        CaretRect = caretRect;
        Caret = caret;
    }

    /// <summary>The slice of the layout that is on screen, including the scroll offset.</summary>
    public LayoutViewport Viewport { get; }

    /// <summary>The layout the frame was built from.</summary>
    public CellLayout Layout => Viewport.Layout;

    /// <summary>The measure that produced the geometry, needed to draw text at the same widths.</summary>
    public ITextMeasure Measure { get; }

    /// <summary>
    /// The decorations to draw, in document order and before the text, so a background or a border is never
    /// painted over the content it belongs to.
    /// </summary>
    /// <remarks>
    /// A painter draws these first and the text runs after. Each entry is a region and what it asks for, never a
    /// colour: the palette turns a <see cref="Projectional.Language.PaintRole"/> into a brush, so a notation that
    /// boxes a table gets a box in whatever palette the host chose.
    /// </remarks>
    public IReadOnlyList<PlacedPaint> Paints { get; }

    /// <summary>
    /// The pictures to draw, in document order, after the decorations and before the text.
    /// </summary>
    /// <remarks>
    /// A picture is a region and the key the notation wrote, never a bitmap: the host decides what a key looks
    /// like, so the same document draws its icons from the artwork of whichever host shows it - and a key the host
    /// has no artwork for is drawn as a placeholder, which is what keeps a missing picture visible rather than
    /// silent.
    /// </remarks>
    public IReadOnlyList<PlacedImage> Images { get; }

    /// <summary>The text to draw, in document order.</summary>
    /// <remarks>
    /// Only cells that carry text are here, and only cells the viewport can see, so a renderer can draw
    /// the list straight through without clipping decisions of its own beyond the window itself.
    /// </remarks>
    public IReadOnlyList<TextRun> TextRuns { get; }

    /// <summary>The boxes to highlight, empty when nothing is selected.</summary>
    public IReadOnlyList<LayoutRect> Highlights { get; }

    /// <summary>
    /// The zero-width bar marking the caret, or <see langword="null"/> when the caret names no cell.
    /// </summary>
    public LayoutRect? CaretRect { get; }

    /// <summary>The position the caret was drawn at.</summary>
    public CaretPosition Caret { get; }

    /// <summary>Builds the frame for one viewport and one selection.</summary>
    /// <param name="layout">The layout to draw from.</param>
    /// <param name="viewport">How much of it fits on screen.</param>
    /// <param name="offsetX">The horizontal scroll offset, clamped by the viewport.</param>
    /// <param name="offsetY">The vertical scroll offset, clamped by the viewport.</param>
    /// <param name="selection">Where the caret is and what it is holding.</param>
    /// <param name="measure">The measure that turns text into distances.</param>
    /// <param name="styler">
    /// What each cell's text means, defaulting to <see cref="CellStyler.Default"/>; it decides the
    /// <see cref="TextStyleKind"/> on every run and nothing else, since text is measured before it is
    /// styled.
    /// </param>
    public static EditorFrame Build(
        CellLayout layout,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        Selection selection,
        ITextMeasure measure,
        ICellStyler? styler = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        var window = LayoutViewport.Of(layout, viewport, offsetX, offsetY);
        var places = CaretPlaces.Of(layout.Tree);

        return new EditorFrame(
            window,
            measure,
            PaintsOf(window),
            ImagesOf(window),
            TextRunsOf(window, layout, styler ?? CellStyler.Default, measure),
            SelectionRects.Of(places, layout, selection, measure),
            CaretBounds.Of(places, layout, selection.Caret, measure),
            selection.Caret);
    }

    /// <summary>The decorations the viewport can see, in document order.</summary>
    /// <remarks>
    /// A cell with no paint, or with a paint that draws nothing, contributes no entry - which is what keeps a
    /// notation that never decorates anything from paying for a list of empty regions.
    /// </remarks>
    internal static List<PlacedPaint> PaintsOf(LayoutViewport viewport)
    {
        var paints = new List<PlacedPaint>();

        foreach (var placed in viewport.Cells)
        {
            if (placed.Cell.Paint is { IsEmpty: false } paint)
            {
                paints.Add(new PlacedPaint(placed.Cell, placed.Bounds, paint));
            }

            // A table's header is a band behind its first row, so it is asked for after the table's own box - the same
            // region, drawn on top of it - and only a table that named a role for it contributes one.
            if (placed.Cell is TableCell { HeaderFill: { } headerFill } table
                && viewport.Layout.HeaderBandOf(table) is { } band)
            {
                paints.Add(new PlacedPaint(table, band, new CellPaint(fill: headerFill)));
            }
        }

        return paints;
    }

    public override string ToString() =>
        $"{TextRuns.Count} runs, {Images.Count} image(s), {Highlights.Count} highlights, caret at {Caret} in {Viewport}";

    /// <summary>The pictures the viewport can see, in document order.</summary>
    /// <remarks>
    /// A picture is a cell that draws nothing as text, so the frame reports it as a region and a key and leaves the
    /// artwork to the host. Nothing is measured here: the box came from the notation and layout already placed it,
    /// which is what keeps a bitmap out of the frame and the frame out of Avalonia.
    /// </remarks>
    internal static List<PlacedImage> ImagesOf(LayoutViewport viewport)
    {
        var images = new List<PlacedImage>();

        foreach (var placed in viewport.Cells)
        {
            if (placed.Cell is ImageCell image)
            {
                images.Add(new PlacedImage(image, placed.Bounds, image.Key));
            }
        }

        return images;
    }

    /// <summary>One run per drawn line, so a cell that wraps is painted line by line.</summary>
    /// <remarks>
    /// A cell of one line keeps its own bounds rather than a rect measured from its text: the bounds are
    /// what the cell occupies, and a run that shrank to the glyphs would leave the rest of the cell
    /// unpainted. Only a cell of several lines is split, because there is no single rect that covers its
    /// lines without also covering the gap the break left.
    /// </remarks>
    internal static List<TextRun> TextRunsOf(
        LayoutViewport viewport,
        CellLayout layout,
        ICellStyler styler,
        ITextMeasure measure)
    {
        var runs = new List<TextRun>(viewport.Cells.Count);

        foreach (var placed in viewport.Cells)
        {
            if (!CellText.IsVisible(placed.Cell))
            {
                continue;
            }

            var style = styler.StyleOf(placed.Cell);
            var emphasis = placed.Cell is TextCell text ? text.Emphasis : TextEmphasis.None;
            var lines = layout.LinesOf(placed.Cell);

            if (lines is null || lines.IsSingleLine)
            {
                runs.Add(new TextRun(placed.Cell, CellText.Of(placed.Cell), placed.Bounds, style, emphasis));
                continue;
            }

            for (var index = 0; index < lines.LineCount; index++)
            {
                runs.Add(new TextRun(
                    placed.Cell,
                    lines.ContentOf(index),
                    new LayoutRect(
                        placed.Bounds.X,
                        placed.Bounds.Y + (index * lines.LineHeight),
                        lines.Lines[index].Width,
                        lines.LineHeight),
                    style,
                    emphasis));
            }
        }

        return runs;
    }
}
