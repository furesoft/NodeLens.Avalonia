using Projectional.Core;
using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Renders a model, or a single node, into a text grid by laying out the cells its notation
/// produces and drawing every text-bearing cell at the position layout gave it.
/// </summary>
/// <remarks>
/// <para>
/// This is the headless half of the editor: no window, no font, no Avalonia. That is what lets
/// notation be tested as text, and it draws from the same placement data the real control will use,
/// so a snapshot failure is a layout failure rather than a snapshot-only artefact.
/// </para>
/// <para>
/// One layout unit is one character cell and one row, which is exactly what
/// <see cref="MonospaceTextMeasure"/> promises. A cell that carries no text - a container - draws
/// nothing and only ever contributes position, so drawing in document order cannot bury a child
/// under its parent.
/// </para>
/// </remarks>
internal static class LayoutSnapshot
{
    /// <summary>Renders one node exactly as its concept's notation draws it.</summary>
    /// <param name="node">The node to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the snapshot has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    /// <param name="types">
    /// The language's type rules, which a <see cref="TypeOfNotationCell"/> draws. Omitting them renders the annotation
    /// as <c>unknown</c>, which is what a language without rules shows.
    /// </param>
    internal static string Render(
        Node node,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null,
        TypeRegistry? types = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        return Render([LayoutOf(node, notations, choice, types)], width);
    }

    /// <summary>
    /// Renders every root of <paramref name="model"/> into at most <paramref name="width"/> columns,
    /// one below the other in document order.
    /// </summary>
    /// <param name="model">The model to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the snapshot has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    /// <param name="types">The language's type rules, which a <see cref="TypeOfNotationCell"/> draws.</param>
    internal static string Render(
        Model model,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null,
        TypeRegistry? types = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        return Render([.. model.Roots.Select(root => LayoutOf(root, notations, choice, types))], width);
    }

    private static CellLayout LayoutOf(
        Node node,
        NotationRegistry notations,
        INotationChoice? choice,
        TypeRegistry? types,
        bool annotations = true) =>
        new CellLayouter(MonospaceTextMeasure.Instance).Layout(
            new EditorTreeBuilder(notations, choice: choice, types: types, annotations: annotations).Build(node));

    /// <summary>Renders a layout that has already been produced, e.g. the one an edit session holds.</summary>
    /// <param name="layout">The layout to draw.</param>
    /// <param name="width">How many columns the snapshot has.</param>
    internal static string Render(CellLayout layout, int width)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        return Render([layout], width);
    }

    private static string Render(IReadOnlyList<CellLayout> layouts, int width)
    {
        var rowOffsets = Stack(layouts, out var height);

        // A grid needs at least one row, so an empty model renders through a blank one and comes
        // out of ToString as the empty string.
        var grid = new SnapshotGrid(width, Math.Max(1, height));

        for (var i = 0; i < layouts.Count; i++)
        {
            Draw(grid, layouts[i], rowOffsets[i], width);
        }

        return grid.ToString();
    }

    /// <summary>Where each layout starts, and how tall the stacked picture is.</summary>
    /// <remarks>
    /// Roots are stacked with no gap between them: vertical spacing is something a notation decides, so the renderer
    /// must not invent any between two documents. Both outputs stack the same way because both ask this.
    /// </remarks>
    private static int[] Stack(IReadOnlyList<CellLayout> layouts, out int height)
    {
        var offsets = new int[layouts.Count];
        var bottom = 0;

        for (var i = 0; i < layouts.Count; i++)
        {
            offsets[i] = bottom;
            bottom += Rows(layouts[i]);
        }

        height = bottom;

        return offsets;
    }

    /// <summary>Renders one node as classified runs instead of characters, for a caller that wants to colour it.</summary>
    /// <param name="node">The node to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the picture has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    /// <param name="types">The language's type rules, which a <see cref="TypeOfNotationCell"/> draws.</param>
    /// <returns>The runs in the order they are drawn, which is the layout's own order.</returns>
    /// <remarks>
    /// The runs carry the position the renderer put them at and the classification <see cref="CellStyler"/> gives them,
    /// which is the same pair a painter uses. Nothing here knows about colour: a caller decides what a keyword looks
    /// like, exactly as a palette does in the editor.
    /// </remarks>
    internal static IReadOnlyList<SnapshotRun> Runs(
        Node node,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null,
        TypeRegistry? types = null,
        bool annotations = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        return Runs([LayoutOf(node, notations, choice, types, annotations)], width);
    }

    /// <summary>Renders every root of <paramref name="model"/> as classified runs.</summary>
    /// <param name="model">The model to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the picture has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    /// <param name="types">The language's type rules, which a <see cref="TypeOfNotationCell"/> draws.</param>
    internal static IReadOnlyList<SnapshotRun> Runs(
        Model model,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null,
        TypeRegistry? types = null,
        bool annotations = true)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        return Runs([.. model.Roots.Select(root => LayoutOf(root, notations, choice, types, annotations))], width);
    }

    /// <summary>Walks the layouts into a run collector, with the same rows the character grid would use.</summary>
    private static IReadOnlyList<SnapshotRun> Runs(IReadOnlyList<CellLayout> layouts, int width)
    {
        var rowOffsets = Stack(layouts, out _);
        var runs = new SnapshotRuns();

        for (var i = 0; i < layouts.Count; i++)
        {
            Draw(runs, layouts[i], rowOffsets[i], width);
        }

        return runs.Runs;
    }

    private static int Rows(CellLayout layout)
    {
        var bottom = 0;

        foreach (var placed in layout.Cells)
        {
            bottom = Math.Max(bottom, (int)Math.Ceiling(placed.Bounds.Bottom));
        }

        return bottom;
    }

    private static void Draw(ISnapshotSink sink, CellLayout layout, int rowOffset, int width)
    {
        foreach (var placed in layout.Cells)
        {
            if (CellText.Of(placed.Cell) is not { Length: > 0 } text)
            {
                continue;
            }

            var column = (int)Math.Round(placed.Bounds.X);
            var available = width - column;

            if (available <= 0)
            {
                continue;
            }

            // Text wider than the snapshot is cut, not wrapped: the width was asked for, and this
            // renderer places no page edge of its own for anything to wrap at.
            if (layout.LinesOf(placed.Cell) is not { } lines)
            {
                sink.Write(rowOffset + (int)Math.Round(placed.Bounds.Y), column, Cut(text, available), CellStyler.Default.StyleOf(placed.Cell));
                continue;
            }

            // One line draws the cell's own text, which is not always the text the layout wrapped: a property
            // that shows a placeholder instead of its empty value is one line of a text the model does not
            // hold, and slicing the value's lines would draw nothing at all.
            if (lines.IsSingleLine)
            {
                sink.Write(rowOffset + (int)Math.Round(placed.Bounds.Y), column, Cut(text, available), CellStyler.Default.StyleOf(placed.Cell));
                continue;
            }

            for (var index = 0; index < lines.LineCount; index++)
            {
                var line = lines.Lines[index];

                // A blank line draws nothing, and the row it occupies is already accounted for by the
                // height of the cell.
                if (line.IsEmpty)
                {
                    continue;
                }

                var row = rowOffset + (int)Math.Round(placed.Bounds.Y + (index * lines.LineHeight));

                sink.Write(row, column, Cut(text.Substring(line.Start, line.ContentLength), available), CellStyler.Default.StyleOf(placed.Cell));
            }
        }
    }

    private static string Cut(string text, int available) => text.Length > available ? text[..available] : text;
}
