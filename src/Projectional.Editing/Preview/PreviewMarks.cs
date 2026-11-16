using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// What a ghost draws differently from the page: the cell marks a diff produces, plus the counts a badge says.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is the only interesting part. A difference names a <em>node</em>, and a node is drawn as several
/// cells - a keyword, a property, a brace - so "this node is new" becomes a mark on each cell whose address the
/// node owns, in document order. A difference that names a <em>slot</em> (a rewritten property, a retargeted
/// reference) becomes a mark on exactly the cell that shows that slot. Ownership is
/// <see cref="CellAddress.Owner"/>, never address equality, because a child's projection root carries the
/// parent's element address.
/// </para>
/// <para>
/// A removal has no cells to mark - the node is gone from the ghost - so it is marked where it used to be: on the
/// slot cell of the parent it was taken out of. The removed text travels as
/// <see cref="CellChange.OldValue"/>, which is how the tooltip can name what went without keeping the old model
/// alive.
/// </para>
/// <para>
/// <b>Counts count nodes; marks count cells.</b> The two are deliberately different numbers: a badge that said
/// "seven added" because an added rule happens to be drawn with seven cells would be counting the renderer, not
/// the edit.
/// </para>
/// </remarks>
public sealed class PreviewMarks
{
    /// <summary>How many cell marks a ghost draws before it stops marking and says how many it left out.</summary>
    /// <remarks>
    /// Tuning, not contract. The contract is that a large diff still produces a usable ghost: past this many marks
    /// the page would be more tint than text, so the first ones are drawn and the rest are counted.
    /// </remarks>
    public const int CellBudget = 2_000;

    private PreviewMarks(
        IReadOnlyList<CellChange> changes,
        int added,
        int changed,
        int removed,
        int truncated)
    {
        Changes = changes;
        Added = added;
        Changed = changed;
        Removed = removed;
        Truncated = truncated;
    }

    /// <summary>The marks to draw, in document order of the ghost.</summary>
    public IReadOnlyList<CellChange> Changes { get; }

    /// <summary>How many nodes the edit added.</summary>
    public int Added { get; }

    /// <summary>How many existing nodes it changed, counting a node once however many of its cells moved.</summary>
    public int Changed { get; }

    /// <summary>How many nodes it removed.</summary>
    public int Removed { get; }

    /// <summary>How many cell marks were left out because of <see cref="CellBudget"/>.</summary>
    public int Truncated { get; }

    /// <summary>Whether the edit changed nothing at all.</summary>
    public bool IsEmpty => Added == 0 && Changed == 0 && Removed == 0;

    /// <summary>The marks for <paramref name="diff"/>, resolved against the ghost's tree.</summary>
    /// <param name="diff">What the edit did, as node differences.</param>
    /// <param name="ghost">The projection of the sandbox, which is what the marks address.</param>
    /// <param name="budget">How many marks to keep; <see cref="CellBudget"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="diff"/> or <paramref name="ghost"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="budget"/> is negative.</exception>
    /// <remarks>
    /// A difference whose node is not drawn - a concept with no notation, a node inside a collapsed subtree - is
    /// counted and not marked. Counting it anyway is what keeps the badge honest about an edit the ghost cannot
    /// show.
    /// </remarks>
    public static PreviewMarks Of(IReadOnlyList<NodeDifference> diff, EditorTree ghost, int budget = CellBudget)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(ghost);
        ArgumentOutOfRangeException.ThrowIfNegative(budget);

        var cells = ghost.Cells().ToList();
        var marks = new List<CellChange>();
        var added = 0;
        var changed = 0;
        var removed = 0;

        foreach (var difference in diff)
        {
            switch (difference.Kind)
            {
                case NodeDifferenceKind.Added:
                    added++;
                    MarkOwned(cells, difference.Node, CellChangeKind.Added, null, marks);
                    break;

                case NodeDifferenceKind.PropertyChanged:
                case NodeDifferenceKind.ReferenceChanged:
                    changed++;
                    MarkSlot(ghost, difference, CellChangeKind.Changed, marks);
                    break;

                case NodeDifferenceKind.Moved:
                    changed++;
                    MarkOwned(cells, difference.Node, CellChangeKind.Changed, difference.OldValue, marks);
                    break;

                case NodeDifferenceKind.Removed:
                    removed++;
                    MarkRemoval(ghost, difference, marks);
                    break;

                default:
                    break;
            }
        }

        var kept = marks.Count > budget ? marks.GetRange(0, budget) : marks;

        return new PreviewMarks(kept, added, changed, removed, marks.Count - kept.Count);
    }

    /// <summary>Marks every cell the node owns, which is how "this node is new" is drawn.</summary>
    private static void MarkOwned(
        List<EditorCell> cells,
        NodeId node,
        CellChangeKind kind,
        string? oldValue,
        List<CellChange> marks)
    {
        foreach (var cell in cells)
        {
            if (cell.Address.Owner == node)
            {
                marks.Add(new CellChange(cell.Address, kind, oldValue));
            }
        }
    }

    /// <summary>Marks the one cell that shows the slot a property or reference difference names.</summary>
    private static void MarkSlot(
        EditorTree ghost,
        NodeDifference difference,
        CellChangeKind kind,
        List<CellChange> marks)
    {
        if (difference.Slot is not { } slot)
        {
            return;
        }

        var address = CellAddress.OfSlot(difference.Node, slot);

        if (ghost.Find(address) is not null)
        {
            marks.Add(new CellChange(address, kind, difference.OldValue));
        }
    }

    /// <summary>Marks where a removed node used to be: the slot cell of the parent it was taken out of.</summary>
    private static void MarkRemoval(EditorTree ghost, NodeDifference difference, List<CellChange> marks)
    {
        if (difference.Before is not { Parent: { } parent, Slot: { } slot })
        {
            return;
        }

        var address = CellAddress.OfSlot(parent, slot);

        if (ghost.Find(address) is not null)
        {
            marks.Add(new CellChange(address, CellChangeKind.Removed, difference.OldValue));
        }
    }
}
