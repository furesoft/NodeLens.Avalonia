using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The ranges a selection can have, and the nodes a range covers: a word inside one property's text, the
/// whole of it, everything one node shows, the whole document - and back again.
/// </summary>
/// <remarks>
/// <para>
/// The three ranges exist because a projectional document has three natural units, and which one the
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
/// Every range method returns <see langword="null"/> when the range would not exist: no editable text at
/// the caret, a node whose notation draws nothing a caret can stand on, or a document with no cells at
/// all. A caller widens past a <see langword="null"/> instead of treating it as "nothing to select".
/// </para>
/// <para>
/// <see cref="NodesIn"/> walks the other way, from a range to the nodes it covers, which is what a
/// structural action needs: the range is what the user sees, the nodes are what the model can remove.
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

    /// <summary>The word the caret stands on, or <see langword="null"/> when there is no editable text.</summary>
    /// <param name="context">The apply being run.</param>
    /// <remarks>
    /// <para>
    /// A word is the maximal run of characters of one kind around the offset: letters, digits and
    /// underscores are one kind, whitespace another, and every other character a third. So
    /// <c>total_amount</c> is one word, <c>42.5</c> is three, and a run of punctuation such as
    /// <c>-&gt;</c> is one - the kinds are what a reader would call a word, not what a tokenizer would.
    /// </para>
    /// <para>
    /// The kind is taken from the character after the caret when that character is a letter or a digit,
    /// otherwise from the character before it, otherwise from the character at the caret. That single rule
    /// is what makes a click on either half of a letter select that letter's word: a caret placed after a
    /// word is still "on" the word, because the user pointed at the glyph they just clicked.
    /// </para>
    /// <para>
    /// This is a range <em>inside</em> a value, so a cell with nothing to select answers
    /// <see langword="null"/> rather than falling back to the whole value. Widening is the caller's
    /// decision, never this method's.
    /// </para>
    /// </remarks>
    public static Selection? OfWord(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PlaceAt(context.Selection.Caret) is not { } place ||
            place.Cell is not PropertyCell cell ||
            cell.Text.Length == 0)
        {
            return null;
        }

        var offset = Math.Clamp(context.Selection.Caret.Offset, 0, cell.Text.Length);
        var (start, end) = RunAt(cell.Text, offset);

        return new Selection(CaretPosition.At(place, start), CaretPosition.At(place, end));
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
    /// The run of same-kind characters that <paramref name="offset"/> falls in or beside.
    /// </summary>
    /// <remarks>
    /// <paramref name="offset"/> is a caret offset into a non-empty string, so <c>0</c> and
    /// <paramref name="text"/>'s length are both valid and both mean "the first" and "the last" run.
    /// The answer is never empty: every offset is adjacent to at least one character.
    /// </remarks>
    private static (int Start, int End) RunAt(string text, int offset)
    {
        int index;

        if (offset < text.Length && IsWord(text[offset]))
        {
            index = offset;
        }
        else if (offset > 0 && IsWord(text[offset - 1]))
        {
            index = offset - 1;
        }
        else
        {
            index = Math.Min(offset, text.Length - 1);
        }

        var start = index;
        var end = index + 1;

        while (start > 0 && SameKind(text[start - 1], text[index]))
        {
            start--;
        }

        while (end < text.Length && SameKind(text[end], text[index]))
        {
            end++;
        }

        return (start, end);
    }

    /// <summary>Whether <paramref name="c"/> is a character a word is made of.</summary>
    /// <remarks>
    /// Underscore counts, so <c>total_amount</c> stays one word; a camel hump does not split, because
    /// <c>totalAmount</c> is one identifier to every reader who did not ask for humps.
    /// </remarks>
    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Whether two characters belong to the same kind of run: word, whitespace or punctuation.</summary>
    private static bool SameKind(char left, char right) =>
        IsWord(left) == IsWord(right) && char.IsWhiteSpace(left) == char.IsWhiteSpace(right);

    /// <summary>
    /// The nodes the current selection covers completely, outermost first, in document order.
    /// </summary>
    /// <param name="context">The apply being run.</param>
    /// <remarks>
    /// <para>
    /// The inverse of <see cref="OfNode"/>: a node is covered when every caret place of its subtree lies
    /// between the two ends of the selection. Only the outermost covered node is reported, because a node
    /// whose ancestor is also covered is already on its way out - listing it would delete it twice, and
    /// the second delete would fail on a node that no longer has a parent.
    /// </para>
    /// <para>
    /// The document is never reported even when the whole of it is selected: it is what the session edits,
    /// so removing it would leave the editor with no tree. Its covered children are reported instead, so
    /// selecting everything empties the document rather than closing it. The document is recognised by
    /// identity rather than by <see cref="Node.Parent"/> being null, because a session may well be opened
    /// on a node that hangs inside a larger model.
    /// </para>
    /// <para>
    /// A range that cannot be resolved against the current tree - because the cell it names was rebuilt
    /// away - covers nothing, and the answer is empty rather than an exception. A node that draws no cell
    /// at all is likewise never covered, since there is no place to compare.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Node> NodesIn(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;
        var places = context.Places;

        if (selection.IsEmpty || places.Count == 0)
        {
            return [];
        }

        var anchor = CaretPlaces.Resolve(places, selection.Anchor);
        var caret = CaretPlaces.Resolve(places, selection.Caret);

        if (anchor < 0 || caret < 0)
        {
            return [];
        }

        var start = Math.Min(anchor, caret);
        var end = Math.Max(anchor, caret);
        var spans = Spans(context.Document, places);
        var nodes = new List<Node>();
        var pending = new Stack<Node>();
        pending.Push(context.Document);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (spans.TryGetValue(node.Id, out var span) &&
                span.Min >= start &&
                span.Max <= end &&
                !ReferenceEquals(node, context.Document))
            {
                nodes.Add(node);
                continue;
            }

            foreach (var child in node.ChildrenInDocumentOrder())
            {
                pending.Push(child);
            }
        }

        nodes.Sort((left, right) => spans[left.Id].Min.CompareTo(spans[right.Id].Min));

        return nodes;
    }

    /// <summary>
    /// The range of caret-place indices every node of <paramref name="document"/> occupies, its subtree
    /// included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A node's own cells are interleaved with its children's - a keyword before a child, a bracket after
    /// it - so the answer is the bounding span over the whole subtree rather than a contiguous run of the
    /// node's own id. That is exactly the question "is this node entirely inside the selection" needs,
    /// and it costs one pass over the places plus one post-order pass over the tree.
    /// </para>
    /// <para>
    /// A node that draws nothing has no entry: there is no place to compare, so it can never be covered.
    /// </para>
    /// </remarks>
    private static Dictionary<NodeId, (int Min, int Max)> Spans(
        Node document,
        IReadOnlyList<CaretPlace> places)
    {
        var own = new Dictionary<NodeId, (int Min, int Max)>();

        for (var index = 0; index < places.Count; index++)
        {
            var owner = places[index].Address.Owner;

            if (owner.IsEmpty)
            {
                continue;
            }

            own[owner] = own.TryGetValue(owner, out var seen) ? (seen.Min, index) : (index, index);
        }

        var spans = new Dictionary<NodeId, (int Min, int Max)>();
        var pending = new Stack<(Node Node, bool Expanded)>();
        pending.Push((document, false));

        while (pending.Count > 0)
        {
            var (node, expanded) = pending.Pop();

            if (!expanded)
            {
                pending.Push((node, true));

                foreach (var child in node.ChildrenInDocumentOrder())
                {
                    pending.Push((child, false));
                }

                continue;
            }

            (int Min, int Max)? span = own.TryGetValue(node.Id, out var mine) ? mine : null;

            foreach (var child in node.ChildrenInDocumentOrder())
            {
                if (!spans.TryGetValue(child.Id, out var theirs))
                {
                    continue;
                }

                span = span is { } current
                    ? (Math.Min(current.Min, theirs.Min), Math.Max(current.Max, theirs.Max))
                    : theirs;
            }

            if (span is { } total)
            {
                spans[node.Id] = total;
            }
        }

        return spans;
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
