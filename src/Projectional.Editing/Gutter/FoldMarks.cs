using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Where the fold arrows go: one per foldable node, in the order the document draws them.</summary>
/// <remarks>
/// <para>
/// Two linear passes, and that is the whole design: one document-order walk collecting the ids that may be
/// folded, then one pass over the layout keeping the first placed cell whose address is a node address in
/// that set. Asking <see cref="CellLayout.BoundsOf(CellAddress)"/> once per node instead would nest a scan
/// inside a scan - the quadratic shape [line-numbers.md](../../docs/architecture/line-numbers.md) already
/// avoids for row numbers.
/// </para>
/// <para>
/// A mark is emitted when the node is foldable <em>and</em> folding it would hide something, or when it is
/// already folded. The second half matters: a node that a later edit shrank to one line would otherwise
/// fold invisibly and lose its arrow, leaving no way back except the menu.
/// </para>
/// </remarks>
public static class FoldMarks
{
    /// <summary>The marks of <paramref name="session"/>, in visual order.</summary>
    /// <param name="session">The document to read the foldable nodes from.</param>
    /// <param name="layout">The layout to place them on.</param>
    /// <param name="measure">The measure that says how tall one line is, and how wide the glyph is.</param>
    /// <returns>The marks; empty when the language does not opt in.</returns>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    public static IReadOnlyList<FoldMark> Of(EditorSession session, CellLayout layout, ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        var foldable = FoldableNodes(session.Document);

        if (foldable.Count == 0)
        {
            return [];
        }

        var marks = new List<FoldMark>();
        var lineHeight = measure.LineHeight;
        var side = measure.Measure("+", Language.TextEmphasis.None);
        var placed = new HashSet<NodeId>();

        foreach (var cell in layout.Cells)
        {
            // A node address with no slot is "this node"; anything below it belongs to a child.
            if (cell.Cell.Address.Slot is not null
                || !foldable.Contains(cell.Cell.Address.Owner)
                || !placed.Add(cell.Cell.Address.Owner))
            {
                continue;
            }

            var collapsed = session.Collapsed.Contains(cell.Cell.Address.Owner);

            if (!collapsed && cell.Bounds.Height <= lineHeight)
            {
                continue;
            }

            marks.Add(new FoldMark(
                cell.Cell.Address.Owner,
                new LayoutRect(0, cell.Bounds.Y, side, lineHeight),
                collapsed));
        }

        return marks;
    }

    /// <summary>The nodes of <paramref name="document"/> that may be folded.</summary>
    /// <remarks>
    /// A set, not a list: the visual order comes from the layout pass below, so collecting in document
    /// order here would be work nobody reads.
    /// </remarks>
    private static HashSet<NodeId> FoldableNodes(Node document)
    {
        var foldable = new HashSet<NodeId>();
        var pending = new Stack<Node>();

        pending.Push(document);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (Foldable.Of(node))
            {
                foldable.Add(node.Id);
            }

            foreach (var child in node.ChildrenInDocumentOrder())
            {
                pending.Push(child);
            }
        }

        return foldable;
    }
}
