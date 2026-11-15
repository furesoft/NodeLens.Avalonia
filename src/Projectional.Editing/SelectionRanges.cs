using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The selections a widening gesture can produce: one property's text, everything one node shows, or the
/// whole document.
/// </summary>
/// <remarks>
/// <para>
/// The three answers exist because a projectional document has three natural units, and which one the
/// user means depends on how far the caret has already widened. A value is what a text action can
/// replace; a node is what a structural action can delete or move; the document is what the host can
/// save. Each is a range over caret places, so none of them needs a second notion of selection.
/// </para>
/// <para>
/// A node's cells are contiguous in document order, which is what makes "everything this node shows" a
/// single range: the run from the first caret place belonging to the node to the last. Membership is
/// decided by <see cref="CellAddress.Owner"/> against the node and its descendants rather than by address
/// equality, because a child's projection carries the child's id below its own root - and because the
/// spaces and separators a collection inserts carry the id of the node that owns the collection. A
/// selected list therefore has no holes in it.
/// </para>
/// <para>
/// Every method returns <see langword="null"/> when the range would not exist: no editable text at the
/// caret, a node whose notation draws nothing a caret can stand on, or a document with no cells at all.
/// A caller widens past a <see langword="null"/> instead of treating it as "nothing to select".
/// </para>
/// </remarks>
internal static class SelectionRanges
{
    /// <summary>The whole text of the property cell at the caret, or <see langword="null"/>.</summary>
    /// <param name="context">The apply being run.</param>
    /// <remarks>
    /// Empty text counts as nothing to select: a range of length zero is indistinguishable from a caret,
    /// so reporting it would make the ladder stall on a property the user has not filled in yet.
    /// </remarks>
    public static Selection? OfProperty(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PlaceAt(context.Selection.Caret) is not { } place ||
            place.Cell is not PropertyCell cell ||
            cell.Text.Length == 0)
        {
            return null;
        }

        return new Selection(CaretPosition.At(place, 0), CaretPosition.At(place, cell.Text.Length));
    }

    /// <summary>Everything <paramref name="node"/> shows, or <see langword="null"/> if it shows no cells.</summary>
    /// <param name="context">The apply being run.</param>
    /// <param name="node">The node to select, which is usually the one the caret stands in.</param>
    public static Selection? OfNode(EditContext context, Node node)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(node);

        var owned = IdsOf(node);
        CaretPlace? first = null;
        CaretPlace? last = null;

        foreach (var place in context.Places)
        {
            if (!owned.Contains(place.Address.Owner))
            {
                continue;
            }

            first ??= place;
            last = place;
        }

        return first is { } start && last is { } end
            ? new Selection(CaretPosition.At(start, 0), CaretPosition.At(end, end.Length))
            : null;
    }

    /// <summary>Every cell of the document, or <see langword="null"/> when it has none.</summary>
    /// <param name="context">The apply being run.</param>
    public static Selection? OfDocument(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Places.Count == 0)
        {
            return null;
        }

        var first = context.Places[0];
        var last = context.Places[^1];

        return new Selection(CaretPosition.At(first, 0), CaretPosition.At(last, last.Length));
    }

    /// <summary>
    /// The ids of <paramref name="node"/> and everything below it.
    /// </summary>
    /// <remarks>
    /// An explicit stack rather than recursion, because a deeply nested document is exactly the kind of
    /// input a DSL produces and the depth is the user's, not ours.
    /// </remarks>
    private static HashSet<NodeId> IdsOf(Node node)
    {
        var ids = new HashSet<NodeId> { node.Id };
        var pending = new Stack<Node>();
        pending.Push(node);

        while (pending.Count > 0)
        {
            foreach (var child in pending.Pop().ChildrenInDocumentOrder())
            {
                if (ids.Add(child.Id))
                {
                    pending.Push(child);
                }
            }
        }

        return ids;
    }
}
