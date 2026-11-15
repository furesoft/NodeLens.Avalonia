using Projectional.Core;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>Which cells of a layout a diagnostic is about.</summary>
/// <remarks>
/// <para>
/// A diagnostic names a node, a slot and an index - never a cell, because cells are rebuilt on every change
/// and a check may run on a model nobody is drawing. This is the single place that turns that description
/// into cells, so that everything downstream works on cells and none of it has to know what a diagnostic
/// meant.
/// </para>
/// <para>
/// The rules are chosen so that a finding is never invisible. A finding about a node marks the node's own
/// text - the keywords and values the notation gives the node itself - and not its children, so two
/// findings on a parent and a child do not cover the same line twice. A finding about a slot marks that
/// slot, which is the narrowest thing the diagnostic can name without knowing the notation. Only a finding
/// about a whole node that has no text of its own falls back to its outermost cell, so that a container
/// still gets a line's worth of marker instead of nothing at all.
/// </para>
/// <para>
/// A diagnostic that names a node the layout does not contain resolves to no cells rather than throwing:
/// the model may have been edited since the check ran, and a marker set is not a reason to fail a draw.
/// </para>
/// </remarks>
internal static class MarkerCells
{
    /// <summary>The cells of <paramref name="layout"/> that <paramref name="diagnostic"/> is about.</summary>
    /// <param name="layout">The layout to look in.</param>
    /// <param name="diagnostic">The finding to place.</param>
    /// <remarks>
    /// The answer is in document order and contains no duplicates. It is empty only when nothing in the
    /// layout carries <see cref="Diagnostic.Target"/>.
    /// </remarks>
    internal static IReadOnlyList<EditorCell> Of(CellLayout layout, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (SlotOf(diagnostic) is { } address && layout.Tree.Find(address) is { } slot)
        {
            // A range is counted from the start of one value, so it belongs to the cell showing that value
            // and to nothing below it. Without a range the whole slot is meant: the property, or the child
            // and everything it projects.
            return diagnostic.Range is not null ? [slot] : SubtreeOf(slot);
        }

        var own = OwnCellsOf(layout, diagnostic.Target);

        if (own.Count > 0)
        {
            return own;
        }

        // A node whose notation draws text only in its children - a plain container, or one whose variant
        // hides every value - still has to show that something is wrong with it.
        return FirstOwnCellOf(layout, diagnostic.Target) is { } outermost ? [outermost] : [];
    }

    private static CellAddress? SlotOf(Diagnostic diagnostic) => diagnostic.Role switch
    {
        null => null,
        { } role when diagnostic.Index is { } index => CellAddress.OfElement(diagnostic.Target, role, index),
        { } role => CellAddress.OfSlot(diagnostic.Target, role),
    };

    private static List<EditorCell> OwnCellsOf(CellLayout layout, NodeId target)
    {
        var cells = new List<EditorCell>();

        foreach (var placed in layout.Cells)
        {
            if (IsOwn(placed.Cell, target) && CellText.IsVisible(placed.Cell))
            {
                cells.Add(placed.Cell);
            }
        }

        return cells;
    }

    private static EditorCell? FirstOwnCellOf(CellLayout layout, NodeId target)
    {
        foreach (var placed in layout.Cells)
        {
            if (IsOwn(placed.Cell, target))
            {
                return placed.Cell;
            }
        }

        return null;
    }

    private static bool IsOwn(EditorCell cell, NodeId target) =>
        cell.Address.Owner == target && !cell.Address.IsElement;

    private static List<EditorCell> SubtreeOf(EditorCell root)
    {
        var cells = new List<EditorCell>();
        Collect(root, cells);

        return cells;
    }

    private static void Collect(EditorCell cell, List<EditorCell> cells)
    {
        if (CellText.IsVisible(cell))
        {
            cells.Add(cell);
        }

        foreach (var child in cell.Children)
        {
            Collect(child, cells);
        }
    }
}
