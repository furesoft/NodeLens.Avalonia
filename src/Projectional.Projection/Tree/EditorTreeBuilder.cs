using Projectional.Core;
using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Turns a node and its concept's notation into an <see cref="EditorTree"/>.
/// </summary>
/// <remarks>
/// <para>
/// Building is a single recursive pass with no lookahead: the notation says what to show, the node
/// says what is there, and the result is a tree in which every cell has exactly one place. That is
/// why the builder assembles bottom-up — parents are known when a cell is constructed, so no fix-up
/// pass is needed and a cell can never end up in two places.
/// </para>
/// <para>
/// Addresses come from the model, never from the shape of the notation. A cell that shows a slot gets
/// the slot address of the node that declares it, a cell that shows one element of a list gets the
/// element address of the <em>parent</em>, and the containers a notation inserts in between carry the
/// node address. This is what makes rebuilding harmless: adding a keyword to a notation changes the
/// tree without moving a single address, so the caret stays where the user left it.
/// </para>
/// <para>
/// A concept with no notation is not an error here. The builder puts a
/// <see cref="MissingNotationCell"/> in its place, so a language can be developed concept by concept
/// and a model that uses a concept whose notation is not written yet still opens.
/// </para>
/// </remarks>
public sealed class EditorTreeBuilder
{
    private readonly NotationRegistry _notations;
    private readonly ICellEmphasis? _emphasis;
    private readonly INotationChoice? _choice;
    private readonly IReadOnlySet<NodeId>? _collapsed;
    private readonly TypeRegistry _types;
    private readonly bool _annotations;

    // What a previous build already produced, so a rebuild can hand back the subtrees the change did not touch.
    private readonly ProjectionCache _cache = new();

    // The nodes the change invalidated for the build in progress, or null when the whole tree is rebuilt.
    private IReadOnlySet<NodeId>? _dirty;

    // Whether the subtree being built shows a reference or a type annotation - a value it does not own and a
    // change elsewhere can therefore invalidate. Such a subtree is never cached.
    private bool _showsExternal;

    /// <summary>Builds trees with every concept drawn as <paramref name="choice"/> asks, default variants when it says nothing.</summary>
    /// <param name="notations">What each concept is drawn with.</param>
    /// <param name="emphasis">What decorates a cell besides its concept's notation, or <see langword="null"/> for nothing.</param>
    /// <param name="choice">Which notation variant to draw, or <see langword="null"/> for each concept's default.</param>
    /// <param name="collapsed">The nodes whose children are not built, or <see langword="null"/> for none.</param>
    /// <param name="types">
    /// The language's type rules, which is what a <see cref="TypeOfNotationCell"/> draws. Empty when omitted, and an
    /// annotation then says <c>unknown</c> - the honest answer for a language that declared none.
    /// </param>
    /// <param name="annotations">
    /// Whether a <see cref="TypeOfNotationCell"/> draws anything at all. <see langword="true"/> by default, because the
    /// annotation is what tells a language author their type rules are missing; a renderer that shows a model as an
    /// <em>example</em> rather than as a state of the editor turns it off, and the cell is then not built rather than
    /// built empty - the notation around it (a colon, its spaces) belongs to the annotation and goes with it.
    /// </param>
    public EditorTreeBuilder(
        NotationRegistry notations,
        ICellEmphasis? emphasis = null,
        INotationChoice? choice = null,
        IReadOnlySet<NodeId>? collapsed = null,
        TypeRegistry? types = null,
        bool annotations = true)
    {
        ArgumentNullException.ThrowIfNull(notations);
        _notations = notations;
        _emphasis = emphasis;
        _choice = choice;
        _collapsed = collapsed;
        _types = types ?? TypeRegistry.Empty;
        _annotations = annotations;
    }

    /// <summary>Builds the tree for <paramref name="node"/>, rebuilding every projection.</summary>
    public EditorTree Build(Node node) => Build(node, null);

    /// <summary>
    /// Builds the tree for <paramref name="node"/>, reusing the projections the change did not invalidate.
    /// </summary>
    /// <param name="node">The node to project.</param>
    /// <param name="dirty">
    /// The nodes whose projection the change invalidated, or <see langword="null"/> to rebuild everything.
    /// A node that is not in the set and whose stored projection still stands is handed back instead of being
    /// built again, so a change rebuilds the path from the root to what it touched and nothing beside it.
    /// </param>
    public EditorTree Build(Node node, IReadOnlySet<NodeId>? dirty)
    {
        ArgumentNullException.ThrowIfNull(node);

        _dirty = dirty;
        _showsExternal = false;

        try
        {
            return new EditorTree(node, BuildNode(node, new CellAddress(node.Id), TextEmphasis.None));
        }
        finally
        {
            _dirty = null;
        }
    }

    /// <summary>Forgets every projection this builder kept, so the next build starts from notation.</summary>
    public void ClearCache() => _cache.Clear();

    /// <summary>
    /// Builds one node, or hands back the projection a previous build kept for it when the change did not
    /// invalidate it. <paramref name="inherited"/> is what the nodes above already asked for, and it is passed
    /// on to every cell of this node so that a decoration applies to a whole subtree.
    /// </summary>
    private EditorCell BuildNode(Node node, CellAddress rootAddress, TextEmphasis inherited)
    {
        var emphasis = inherited | (_emphasis?.EmphasisOf(node) ?? TextEmphasis.None);

        if (_dirty is not null &&
            !_dirty.Contains(node.Id) &&
            _cache.TryReuse(node.Id, rootAddress, emphasis, out var reused))
        {
            return reused;
        }

        var outer = _showsExternal;
        _showsExternal = false;

        var cell = BuildFresh(node, rootAddress, emphasis);

        var shows = _showsExternal;
        _showsExternal = outer | shows;

        if (shows)
        {
            // A projection kept from an earlier build would still show the value this node no longer owns, so
            // it is dropped rather than replaced: nothing may be reused for a subtree that shows one.
            _cache.Forget(node.Id);
        }
        else
        {
            _cache.Store(node.Id, rootAddress, emphasis, cell);
        }

        return cell;
    }

    /// <summary>Builds a node's projection from its notation, without consulting the cache.</summary>
    private EditorCell BuildFresh(Node node, CellAddress rootAddress, TextEmphasis emphasis)
    {
        var notation = _notations.FindFor(node.Concept, _choice);

        if (notation is null)
        {
            return new MissingNotationCell(rootAddress, node.Concept.Key);
        }

        // Substituted after the lookup on purpose: a concept whose notation is missing keeps its
        // `<no notation for ...>` marker, because a language fault must never be tucked behind a fold.
        if (_collapsed?.Contains(node.Id) is true && Foldable.Of(node))
        {
            return new CollapsedCell(rootAddress, CollapsedSummary.Of(node));
        }

        return BuildBlock(notation.Root, node, emphasis, rootAddress);
    }

    /// <summary>
    /// Marks the subtree being built as showing a value the node does not own, so it is never cached.
    /// </summary>
    private EditorCell ShowExternal(EditorCell cell)
    {
        _showsExternal = true;

        return cell;
    }

    /// <summary>
    /// Builds a notation's block cell. <paramref name="rootAddress"/> is set only for the root of a
    /// node's projection, where the cell has to keep the address it was given instead of deriving one
    /// from the slot it shows.
    /// </summary>
    private EditorCell BuildBlock(
        BlockNotationCell cell,
        Node node,
        TextEmphasis emphasis,
        CellAddress? rootAddress = null)
    {
        var address = rootAddress ?? new CellAddress(node.Id);

        return cell switch
        {
            LineNotationCell line => new ContainerCell(
                address,
                CellOrientation.Horizontal,
                BuildInline(line.Content, node, emphasis)),
            SequenceNotationCell sequence => new ContainerCell(
                address,
                CellOrientation.Vertical,
                BuildBlocks(sequence.Content, node, emphasis)),
            IndentNotationCell indent => new ContainerCell(
                address,
                CellOrientation.Vertical,
                isIndented: true,
                BuildBlocks(indent.Content, node, emphasis)),

            // The mirror image of a sequence: the same blocks, the other direction. Layout already knows how to
            // arrange a horizontal run of arbitrary cells, so this is the whole of the projection's part.
            HorizontalNotationCell horizontal => new ContainerCell(
                address,
                CellOrientation.Horizontal,
                BuildBlocks(horizontal.Content, node, emphasis)),
            ChildNotationCell children => BuildChildren(
                children.Slot,
                children.EmptyText,
                node,
                emphasis,
                rootAddress,
                children.Spacing,
                children.Stacked,
                children.Gaps,
                children.TightConcepts),
            CollectionNotationCell collection => BuildCollection(
                collection.Slot, collection.Separator, collection.EmptyText, collection.Spacing, node, emphasis, rootAddress),

            TableNotationCell table => BuildTable(table, node, emphasis, rootAddress),

            // A block that is not filled draws nothing here; `BuildBlocks` is what removes it from the tree, and
            // this case is for a notation whose root is the conditional itself.
            WhenFilledBlockNotationCell block => new ContainerCell(
                address,
                CellOrientation.Vertical,
                BuildBlocks(block.Content, node, emphasis)),

            // The property twin of the case above, with the same division: `BuildBlocks` decides whether the group
            // exists, and this builds it for the root case.
            ConditionalBlockNotationCell conditionalBlock => new ContainerCell(
                address,
                CellOrientation.Vertical,
                BuildBlocks(conditionalBlock.Content, node, emphasis)),
            _ => throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' uses a block cell of unknown kind '{cell.GetType().Name}'."),
        };
    }

    private EditorCell[] BuildBlocks(IReadOnlyList<BlockNotationCell> cells, Node node, TextEmphasis emphasis)
    {
        var built = new List<EditorCell>(cells.Count);

        foreach (var cell in cells)
        {
            // An unfilled block leaves no row: layout gives every vertical cell at least one line, so a notation
            // that wants nothing at all where the block would be has to keep it out of the tree.
            if (cell is WhenFilledBlockNotationCell block && ChildrenOf(node, block).Count == 0)
            {
                continue;
            }

            // The same rule for a property: the group of rows a value chooses is either drawn or absent, never
            // present and empty.
            if (cell is ConditionalBlockNotationCell conditional
                && !string.Equals(node.GetOrNull(conditional.Slot), conditional.Value, StringComparison.Ordinal))
            {
                continue;
            }

            built.Add(BuildBlock(cell, node, emphasis));
        }

        return [.. built];
    }

    /// <summary>The children a when-filled block reads, or a projection error naming a slot that is no child slot.</summary>
    private static IReadOnlyList<Node> ChildrenOf(Node node, WhenFilledBlockNotationCell cell) =>
        node.Concept.FindChild(cell.Slot) is not null
            ? node.GetChildren(cell.Slot)
            : throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' draws blocks while '{cell.Slot}' holds a child, but the concept declares no such child.");

    /// <summary>
    /// Builds a run of inline cells. A cell may answer more than one cell or none at all, which is what a
    /// conditional does, so the result is a list rather than one cell per notation cell.
    /// </summary>
    private EditorCell[] BuildInline(IReadOnlyList<InlineNotationCell> cells, Node node, TextEmphasis emphasis)
    {
        var built = new List<EditorCell>(cells.Count);

        for (var i = 0; i < cells.Count; i++)
        {
            if (!_annotations && cells[i] is TypeOfNotationCell)
            {
                // Everything from the separator to the annotation goes: a notation writes `1 : int`, and dropping the
                // cell alone would leave `1 : ` behind - or, worse, `1  ` when only some of it goes. Spaces and one
                // punctuation directly before it are that separator; anything else before them is the language's own.
                Drop(built, static cell => cell is SpaceCell);
                Drop(built, static cell => cell is ConstantCell { Kind: ConstantKind.Punctuation });
                Drop(built, static cell => cell is SpaceCell);

                continue;
            }

            built.AddRange(BuildInline(cells[i], node, emphasis));
        }

        return [.. built];

        static void Drop(List<EditorCell> cells, Func<EditorCell, bool> matches)
        {
            while (cells.Count > 0 && matches(cells[^1]))
            {
                cells.RemoveAt(cells.Count - 1);
            }
        }
    }

    private EditorCell[] BuildInline(InlineNotationCell cell, Node node, TextEmphasis emphasis) => cell switch
    {
        ConstantNotationCell constant =>
        [
            new ConstantCell(
                new CellAddress(node.Id),
                constant.Text,
                constant.Kind,
                emphasis | constant.Emphasis),
        ],
        SpaceNotationCell => [new SpaceCell(new CellAddress(node.Id))],

        // The type is asked of the registry while the tree is built, exactly like a property value is read from the
        // model: the cell carries finished text, so a renderer needs no registry and an edit that changes the type
        // changes the annotation on the next build.
        TypeOfNotationCell when !_annotations => [],
        TypeOfNotationCell =>
        [
            ShowExternal(
                new ConstantCell(
                    new CellAddress(node.Id),
                    _types.TypeOf(node).ToString() ?? UnknownType.Instance.Name,
                    ConstantKind.Annotation,
                    emphasis)),
        ],
        PropertyNotationCell property => [BuildProperty(property, node, emphasis)],
        ReferenceNotationCell reference => [ShowExternal(BuildReference(reference, node, emphasis))],
        SingleChildNotationCell single =>
            [BuildChildren(single.Slot, single.EmptyText, node, emphasis, rootAddress: null, spacing: 0)],
        InlineCollectionNotationCell inlineCollection =>
            [BuildCollection(
                inlineCollection.Slot, inlineCollection.Separator, inlineCollection.EmptyText, inlineCollection.Spacing, node, emphasis, rootAddress: null)],
        ConditionalNotationCell conditional => BuildConditional(conditional, node, emphasis),
        WhenFilledNotationCell whenFilled => BuildWhenFilled(whenFilled, node, emphasis),
        _ => throw new ProjectionException(
            $"The notation of '{node.Concept.Key}' uses an inline cell of unknown kind '{cell.GetType().Name}'."),
    };

    /// <summary>
    /// Builds one inline cell where the notation promises exactly one - a collection's separator or a table's
    /// header. A conditional is refused there rather than silently dropped, because a gap with no separator
    /// is a different notation than the one that was written.
    /// </summary>
    private EditorCell BuildOneInline(InlineNotationCell cell, Node node, TextEmphasis emphasis)
    {
        var built = BuildInline(cell, node, emphasis);

        return built.Length == 1
            ? built[0]
            : throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' uses a '{cell.GetType().Name}' where exactly one cell has to be drawn, and it draws {built.Length}.");
    }

    /// <summary>
    /// Builds the cells of a conditional, or nothing when its condition does not hold.
    /// </summary>
    /// <remarks>
    /// The property is read as the node reports it - set or defaulted - because a language that writes a default
    /// means it to be the value until somebody changes it. An unset property with no default answers
    /// <see langword="null"/> and therefore never equals the condition's text, which is the right reading of
    /// "nothing is there".
    /// </remarks>
    private EditorCell[] BuildConditional(ConditionalNotationCell conditional, Node node, TextEmphasis emphasis)
    {
        if (node.Concept.FindProperty(conditional.Slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' draws cells when property '{conditional.Slot}' holds a value, but the concept declares no such property.");
        }

        return string.Equals(node.GetOrNull(conditional.Slot), conditional.Value, StringComparison.Ordinal)
            ? BuildInline(conditional.Content, node, emphasis)
            : [];
    }

    /// <summary>
    /// Builds the cells of a "while this slot is filled" run, or nothing when it holds no child.
    /// </summary>
    /// <remarks>
    /// The slot is checked here as well as at registration because a tree is built for a concept the registry may
    /// have validated against another notation - the same defensive read <see cref="BuildConditional"/> makes.
    /// </remarks>
    private EditorCell[] BuildWhenFilled(WhenFilledNotationCell cell, Node node, TextEmphasis emphasis)
    {
        if (node.Concept.FindChild(cell.Slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' draws cells while '{cell.Slot}' holds a child, but the concept declares no such child.");
        }

        return node.GetChildren(cell.Slot).Count > 0
            ? BuildInline(cell.Content, node, emphasis)
            : [];
    }

    private static PropertyCell BuildProperty(PropertyNotationCell notation, Node node, TextEmphasis emphasis) =>
        BuildProperty(notation.Slot, node, emphasis | notation.Emphasis, notation.EmptyText);

    /// <summary>Projects one property of <paramref name="node"/>, whatever notation asked for it.</summary>
    private static PropertyCell BuildProperty(string slot, Node node, TextEmphasis decoration, string emptyText = "")
    {
        var declaration = node.Concept.FindProperty(slot)
            ?? throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' shows property '{slot}', but the concept declares no such property.");

        return new PropertyCell(
            CellAddress.OfSlot(node.Id, slot),
            slot,
            declaration.Type,
            node.GetOrNull(slot) ?? string.Empty,
            node.IsSet(slot),
            decoration,
            emptyText);
    }

    private static ReferenceCell BuildReference(ReferenceNotationCell notation, Node node, TextEmphasis emphasis) =>
        BuildReferenceCell(node, notation.Slot, notation.UnsetText, emphasis | notation.Emphasis);

    /// <summary>
    /// Projects one reference slot, wherever it is drawn: a notation's <see cref="ReferenceNotationCell"/> or a
    /// table column. The three states are the whole vocabulary - unset, unresolved, resolved.
    /// </summary>
    private static ReferenceCell BuildReferenceCell(Node node, string slot, string unsetText, TextEmphasis emphasis)
    {
        if (node.Concept.FindReference(slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' shows reference '{slot}', but the concept declares no such reference.");
        }

        var address = CellAddress.OfSlot(node.Id, slot);
        var reference = node.GetReference(slot);

        if (reference is not { } stored)
        {
            return new ReferenceCell(address, slot, unsetText, ReferenceState.Unset, emphasis: emphasis);
        }

        if (node.GetReferenceTarget(slot) is not { } target)
        {
            // The text the reference kept is what it was written as, so it is what the reader has to see: the state
            // is what says the target is missing, not a placeholder that throws the text away. `resolveInfo` exists
            // for exactly this - an unresolved reference degrades rather than losing what was typed.
            return new ReferenceCell(
                address,
                slot,
                stored.ResolveInfo is { Length: > 0 } text ? text : "<unresolved>",
                ReferenceState.Unresolved,
                stored.Target,
                emphasis);
        }

        return new ReferenceCell(
            address,
            slot,
            // A node this framework cannot name says where its name lives rather than showing a concept key that reads
            // like "this node is unnamed": the name exists, in an assembly this layer never loads (ADR 0014). Nothing is
            // invented - the text is derived, so it can never reach a file.
            target.Name ?? (target.Concept.NameIsExternal
                ? $"<{target.Concept.Key}: named in code>"
                : $"<{target.Concept.Key}>"),
            ReferenceState.Resolved,
            target.Id,
            emphasis);
    }

    /// <summary>
    /// Builds the cell for a child slot. One method covers both notation cells because they differ
    /// only in where the notation puts them, not in what they project: every matching child becomes a
    /// root at its element address, and the slot's empty text covers the case of none.
    /// </summary>
    private ChildCell BuildChildren(
        string slot,
        string emptyText,
        Node node,
        TextEmphasis emphasis,
        CellAddress? rootAddress,
        int spacing,
        bool stacked = false,
        ListGap gaps = ListGap.None,
        IReadOnlySet<string>? tightConcepts = null)
    {
        if (node.Concept.FindChild(slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' shows child '{slot}', but the concept declares no such child.");
        }

        var children = node.GetChildren(slot);
        var address = rootAddress ?? CellAddress.OfSlot(node.Id, slot);

        // A stacked slot needs no gap cells: a new line is already the gap, and a space cell would become
        // a line of its own under the vertical arrangement.
        var content = new EditorCell[stacked
            ? children.Count
            : children.Count + (Math.Max(0, children.Count - 1) * spacing)];

        var next = 0;

        for (var i = 0; i < children.Count; i++)
        {
            if (!stacked && i > 0)
            {
                for (var space = 0; space < spacing; space++)
                {
                    content[next++] = new SpaceCell(address);
                }
            }

            content[next++] = BuildNode(children[i], CellAddress.OfElement(node.Id, slot, i), emphasis);
        }

        return new ChildCell(address, slot, emptyText, content)
        {
            IsStacked = stacked,
            BlankLineBefore = stacked
                ? GapPositions(children, tightConcepts ?? ChildGroups.Empty, gaps)
                : new HashSet<int>(),
        };
    }

    /// <summary>
    /// The positions a blank line goes above, one per gap the notation asked for.
    /// </summary>
    /// <remarks>
    /// A gap always sits <em>above</em> a position, so an empty list gets none and the position after the last child
    /// stands for the gap below it - the same range the layouter reads. The result is a set of positions and not a
    /// predicate, which is what keeps a blank line out of the tree and away from the caret. Where a <em>group</em> ends
    /// is <see cref="ChildGroups"/>'s decision, the same one the generator asks, so the drawing and the generated text
    /// cannot disagree about it.
    /// </remarks>
    private static HashSet<int> GapPositions(
        IReadOnlyList<Node> children,
        IReadOnlySet<string> tight,
        ListGap gaps)
    {
        var starts = new HashSet<int>();

        if (children.Count == 0 || gaps == ListGap.None)
        {
            return starts;
        }

        if (gaps.HasFlag(ListGap.BeforeFirst))
        {
            starts.Add(0);
        }

        if (gaps.HasFlag(ListGap.AfterLast))
        {
            starts.Add(children.Count);
        }

        if (!gaps.HasFlag(ListGap.BetweenGroups))
        {
            return starts;
        }

        for (var index = 1; index < children.Count; index++)
        {
            if (ChildGroups.IsBoundary(children[index - 1].Concept.Key, children[index].Concept.Key, tight))
            {
                starts.Add(index);
            }
        }

        return starts;
    }

    private CollectionCell BuildCollection(
        string slot,
        InlineNotationCell? separatorNotation,
        string emptyText,
        int spacing,
        Node node,
        TextEmphasis emphasis,
        CellAddress? rootAddress)
    {
        if (node.Concept.FindChild(slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' draws children of '{slot}', but the concept declares no such child.");
        }

        var children = node.GetChildren(slot);
        var items = new EditorCell[children.Count];

        for (var i = 0; i < children.Count; i++)
        {
            items[i] = BuildNode(children[i], CellAddress.OfElement(node.Id, slot, i), emphasis);
        }

        var separators = Array.Empty<EditorCell>();

        if (separatorNotation is { } separator && children.Count > 1)
        {
            separators = new EditorCell[children.Count - 1];

            for (var i = 0; i < separators.Length; i++)
            {
                separators[i] = BuildOneInline(separator, node, emphasis);
            }
        }

        return new CollectionCell(
            rootAddress ?? CellAddress.OfSlot(node.Id, slot),
            slot,
            emptyText,
            items,
            separators,
            spacing);
    }

    /// <summary>
    /// Builds a table: one header cell per column and one cell per column of every row, so that the
    /// grid is complete before layout sees it.
    /// </summary>
    private TableCell BuildTable(
        TableNotationCell notation,
        Node node,
        TextEmphasis emphasis,
        CellAddress? rootAddress)
    {
        var slot = notation.Slot;

        if (node.Concept.FindChild(slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' draws a table over '{slot}', but the concept declares no such child.");
        }

        var address = rootAddress ?? CellAddress.OfSlot(node.Id, slot);
        var rows = node.GetChildren(slot);

        // Headers belong to the table's notation, so they are projected once, from the node that
        // owns the table - never from a row, which may not exist at all. That is also why an empty
        // table still draws them: the header is the only thing that says what the columns are.
        var headers = new EditorCell[notation.Columns.Count];

        for (var i = 0; i < headers.Length; i++)
        {
            headers[i] = BuildOneInline(notation.Columns[i].Header, node, emphasis);
        }

        var built = new EditorCell[rows.Count][];

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var cells = new EditorCell[notation.Columns.Count];

            for (var c = 0; c < cells.Length; c++)
            {
                cells[c] = BuildColumnCell(notation.Columns[c], row, emphasis);
            }

            built[r] = cells;
        }

        return new TableCell(address, slot, notation.EmptyText, headers, built, CellAddress.OfSlot(node.Id, slot));
    }

    /// <summary>Projects one column of one row, which is a property, a single child or a reference of the row.</summary>
    private EditorCell BuildColumnCell(TableColumn column, Node row, TextEmphasis emphasis)
    {
        if (row.Concept.FindProperty(column.Slot) is not null)
        {
            // A column already says what an empty cell shows for a child slot, so it says it for a property too.
            return BuildProperty(column.Slot, row, emphasis, column.EmptyText);
        }

        if (row.Concept.FindChild(column.Slot) is { } child)
        {
            if (child.Cardinality != Cardinality.One)
            {
                throw new ProjectionException(
                    $"The table of '{row.Concept.Key}' shows column '{column.Slot}', but that child holds many; a cell can only show one child.");
            }

            return BuildChildren(column.Slot, column.EmptyText, row, emphasis, rootAddress: null, spacing: 0);
        }

        // One implementation for both paths: a reference cell answers the same three states wherever it is
        // drawn, so a table column must not be a second reading of a reference.
        if (row.Concept.FindReference(column.Slot) is not null)
        {
            return ShowExternal(BuildReferenceCell(row, column.Slot, column.EmptyText, emphasis));
        }

        throw new ProjectionException(
            $"The table of '{row.Concept.Key}' shows column '{column.Slot}', but the concept declares no property, child or reference of that name.");
    }
}
