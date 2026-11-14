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

    public EditorTreeBuilder(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);
        _notations = notations;
    }

    /// <summary>Builds the tree for <paramref name="node"/>.</summary>
    public EditorTree Build(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new EditorTree(node, BuildNode(node, new CellAddress(node.Id)));
    }

    private EditorCell BuildNode(Node node, CellAddress rootAddress)
    {
        var notation = _notations.FindFor(node.Concept);

        return notation is null
            ? new MissingNotationCell(rootAddress, node.Concept.Key)
            : BuildBlock(notation.Root, node, rootAddress);
    }

    /// <summary>
    /// Builds a notation's block cell. <paramref name="rootAddress"/> is set only for the root of a
    /// node's projection, where the cell has to keep the address it was given instead of deriving one
    /// from the slot it shows.
    /// </summary>
    private EditorCell BuildBlock(BlockNotationCell cell, Node node, CellAddress? rootAddress = null)
    {
        var address = rootAddress ?? new CellAddress(node.Id);

        return cell switch
        {
            LineNotationCell line => new ContainerCell(address, CellOrientation.Horizontal, BuildInline(line.Content, node)),
            SequenceNotationCell sequence => new ContainerCell(address, CellOrientation.Vertical, BuildBlocks(sequence.Content, node)),
            IndentNotationCell indent => new ContainerCell(address, CellOrientation.Vertical, isIndented: true, BuildBlocks(indent.Content, node)),
            ChildNotationCell children => BuildChildren(children.Slot, children.EmptyText, node, rootAddress),
            CollectionNotationCell collection => BuildCollection(collection, node, rootAddress),
            _ => throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' uses a block cell of unknown kind '{cell.GetType().Name}'."),
        };
    }

    private EditorCell[] BuildBlocks(IReadOnlyList<BlockNotationCell> cells, Node node)
    {
        var built = new EditorCell[cells.Count];

        for (var i = 0; i < cells.Count; i++)
        {
            built[i] = BuildBlock(cells[i], node);
        }

        return built;
    }

    private EditorCell[] BuildInline(IReadOnlyList<InlineNotationCell> cells, Node node)
    {
        var built = new EditorCell[cells.Count];

        for (var i = 0; i < cells.Count; i++)
        {
            built[i] = BuildInline(cells[i], node);
        }

        return built;
    }

    private EditorCell BuildInline(InlineNotationCell cell, Node node) => cell switch
    {
        ConstantNotationCell constant => new ConstantCell(new CellAddress(node.Id), constant.Text, constant.Kind),
        SpaceNotationCell => new SpaceCell(new CellAddress(node.Id)),
        PropertyNotationCell property => BuildProperty(property.Slot, node),
        ReferenceNotationCell reference => BuildReference(reference, node),
        SingleChildNotationCell single => BuildChildren(single.Slot, single.EmptyText, node, rootAddress: null),
        _ => throw new ProjectionException(
            $"The notation of '{node.Concept.Key}' uses an inline cell of unknown kind '{cell.GetType().Name}'."),
    };

    private static PropertyCell BuildProperty(string slot, Node node)
    {
        var declaration = node.Concept.FindProperty(slot)
            ?? throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' shows property '{slot}', but the concept declares no such property.");

        return new PropertyCell(
            CellAddress.OfSlot(node.Id, slot),
            slot,
            declaration.Type,
            node.GetOrNull(slot) ?? string.Empty,
            node.IsSet(slot));
    }

    private static ReferenceCell BuildReference(ReferenceNotationCell notation, Node node)
    {
        var slot = notation.Slot;

        if (node.Concept.FindReference(slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' shows reference '{slot}', but the concept declares no such reference.");
        }

        var address = CellAddress.OfSlot(node.Id, slot);
        var reference = node.GetReference(slot);

        if (reference is not { } stored)
        {
            return new ReferenceCell(address, slot, notation.UnsetText, ReferenceState.Unset);
        }

        if (node.GetReferenceTarget(slot) is not { } target)
        {
            return new ReferenceCell(address, slot, "<unresolved>", ReferenceState.Unresolved, stored.Target);
        }

        return new ReferenceCell(address, slot, target.Name ?? $"<{target.Concept.Key}>", ReferenceState.Resolved, target.Id);
    }

    /// <summary>
    /// Builds the cell for a child slot. One method covers both notation cells because they differ
    /// only in where the notation puts them, not in what they project: every matching child becomes a
    /// root at its element address, and the slot's empty text covers the case of none.
    /// </summary>
    private ChildCell BuildChildren(string slot, string emptyText, Node node, CellAddress? rootAddress)
    {
        if (node.Concept.FindChild(slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' shows child '{slot}', but the concept declares no such child.");
        }

        var children = node.GetChildren(slot);
        var content = new EditorCell[children.Count];

        for (var i = 0; i < children.Count; i++)
        {
            content[i] = BuildNode(children[i], CellAddress.OfElement(node.Id, slot, i));
        }

        return new ChildCell(rootAddress ?? CellAddress.OfSlot(node.Id, slot), slot, emptyText, content);
    }

    private CollectionCell BuildCollection(CollectionNotationCell notation, Node node, CellAddress? rootAddress)
    {
        var slot = notation.Slot;

        if (node.Concept.FindChild(slot) is null)
        {
            throw new ProjectionException(
                $"The notation of '{node.Concept.Key}' draws children of '{slot}', but the concept declares no such child.");
        }

        var children = node.GetChildren(slot);
        var items = new EditorCell[children.Count];

        for (var i = 0; i < children.Count; i++)
        {
            items[i] = BuildNode(children[i], CellAddress.OfElement(node.Id, slot, i));
        }

        var separators = new EditorCell[Math.Max(0, children.Count - 1)];

        for (var i = 0; i < separators.Length; i++)
        {
            separators[i] = notation.Separator is { } separator
                ? BuildInline(separator, node)
                : new SpaceCell(new CellAddress(node.Id));
        }

        return new CollectionCell(
            rootAddress ?? CellAddress.OfSlot(node.Id, slot),
            slot,
            notation.EmptyText,
            items,
            separators);
    }
}
