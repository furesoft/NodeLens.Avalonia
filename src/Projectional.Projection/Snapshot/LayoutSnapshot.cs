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
    internal static string Render(Node node, NotationRegistry notations, int width)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        return Render([LayoutOf(node, notations)], width);
    }

    /// <summary>
    /// Renders every root of <paramref name="model"/> into at most <paramref name="width"/> columns,
    /// one below the other in document order.
    /// </summary>
    internal static string Render(Model model, NotationRegistry notations, int width)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

        return Render([.. model.Roots.Select(root => LayoutOf(root, notations))], width);
    }

    private static CellLayout LayoutOf(Node node, NotationRegistry notations) =>
        new CellLayouter(MonospaceTextMeasure.Instance).Layout(new EditorTreeBuilder(notations).Build(node));

    private static string Render(IReadOnlyList<CellLayout> layouts, int width)
    {
        // Roots are stacked with no gap between them: vertical spacing is something a notation
        // decides, so the renderer must not invent any between two documents.
        var rowOffsets = new int[layouts.Count];
        var height = 0;

        for (var i = 0; i < layouts.Count; i++)
        {
            rowOffsets[i] = height;
            height += Rows(layouts[i]);
        }

        // A grid needs at least one row, so an empty model renders through a blank one and comes
        // out of ToString as the empty string.
        var grid = new SnapshotGrid(width, Math.Max(1, height));

        for (var i = 0; i < layouts.Count; i++)
        {
            Draw(grid, layouts[i], rowOffsets[i], width);
        }

        return grid.ToString();
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

    private static void Draw(SnapshotGrid grid, CellLayout layout, int rowOffset, int width)
    {
        foreach (var placed in layout.Cells)
        {
            // A text cell is one line tall, so the height layout reported only ever moves it.
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

            // Text wider than the snapshot is cut, not wrapped: the width was asked for.
            var visible = text.Length > available ? text[..available] : text;
            grid.Write(rowOffset + (int)Math.Round(placed.Bounds.Y), column, visible);
        }
    }
}
