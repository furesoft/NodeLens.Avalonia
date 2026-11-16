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

        // One pass over the layout building address -> bounds, because a node's cell is found by address and
        // asking BoundsOf per node would nest a scan inside a scan. The first cell wins, which is the
        // outermost - exactly the box a mark belongs on.
        var bounds = new Dictionary<CellAddress, LayoutRect>(layout.Cells.Count);

        foreach (var cell in layout.Cells)
        {
            bounds.TryAdd(cell.Cell.Address, cell.Bounds);
        }

        var marks = new List<FoldMark>();
        var lineHeight = measure.LineHeight;
        var side = measure.Measure("+", Language.TextEmphasis.None);

        foreach (var (node, address) in foldable)
        {
            if (!bounds.TryGetValue(address, out var box))
            {
                continue;
            }

            var collapsed = session.Collapsed.Contains(node);

            if (!collapsed && box.Height <= lineHeight)
            {
                continue;
            }

            marks.Add(new FoldMark(node, new LayoutRect(0, box.Y, side, lineHeight), collapsed));
        }

        marks.Sort((left, right) => left.Bounds.Y.CompareTo(right.Bounds.Y));

        return marks;
    }

    /// <summary>The foldable nodes of <paramref name="document"/>, each with the address its cell carries.</summary>
    /// <remarks>
    /// The address is the node's <em>own</em> only for the document root. Every other node is projected under
    /// its parent's element address - <see cref="CellAddress.OfElement"/> - because an address says where a
    /// cell sits in its parent, not which node it draws. Assuming the node's own address was a bug the
    /// runtime probe caught: the list came back empty and the gutter column stayed zero-width.
    /// </remarks>
    private static List<(NodeId Node, CellAddress Address)> FoldableNodes(Node document)
    {
        var foldable = new List<(NodeId, CellAddress)>();
        var pending = new Stack<Node>();

        pending.Push(document);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (Foldable.Of(node))
            {
                foldable.Add((node.Id, AddressOf(node)));
            }

            foreach (var child in node.ChildrenInDocumentOrder())
            {
                pending.Push(child);
            }
        }

        return foldable;
    }

    /// <summary>The address the cell of <paramref name="node"/> carries.</summary>
    private static CellAddress AddressOf(Node node)
    {
        if (node.Parent is not { } parent)
        {
            return new CellAddress(node.Id);
        }

        foreach (var slot in parent.Concept.Slots)
        {
            if (slot is not ChildSlot child)
            {
                continue;
            }

            var siblings = parent.GetChildren(child.Name);

            for (var index = 0; index < siblings.Count; index++)
            {
                if (siblings[index].Id == node.Id)
                {
                    return CellAddress.OfElement(parent.Id, child.Name, index);
                }
            }
        }

        // A node that is not a child of its parent's slots cannot be placed; its own address is the only
        // honest answer, and a mark that misses is better than a throw during a paint.
        return new CellAddress(node.Id);
    }
}
