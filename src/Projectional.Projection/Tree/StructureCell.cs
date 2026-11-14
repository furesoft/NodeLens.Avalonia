namespace Projectional.Projection;

/// <summary>
/// A cell that arranges other cells. Everything structural in the tree derives from this, so that
/// walking the tree never needs to know whether it is looking at a container, a child list or a
/// collection.
/// </summary>
/// <remarks>
/// Content is fixed at construction and every cell in it is claimed as a child, because a runtime
/// tree is a tree: a cell that appeared twice would make the caret's position ambiguous and the
/// parent links meaningless. Assembling bottom-up means the builder never has to fix up parents
/// afterwards.
/// </remarks>
public abstract class StructureCell : EditorCell
{
    private protected StructureCell(CellAddress address, params ReadOnlySpan<EditorCell> content)
        : base(address)
    {
        var cells = new EditorCell[content.Length];

        for (var i = 0; i < content.Length; i++)
        {
            var cell = content[i];
            ArgumentNullException.ThrowIfNull(cell, nameof(content));

            cell.Parent = cell.Parent is null
                ? this
                : throw new ProjectionException(
                    $"The cell {cell} already sits in a tree and cannot be placed as {this} as well.");

            cells[i] = cell;
        }

        Content = cells;
    }

    /// <summary>The cells arranged by this one, in document order.</summary>
    public IReadOnlyList<EditorCell> Content { get; }

    public override IReadOnlyList<EditorCell> Children => Content;
}
