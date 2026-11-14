namespace Projectional.Projection;

/// <summary>
/// One cell of a node's projection: a piece of text, a value slot, a child subtree or a container
/// that arranges others.
/// </summary>
/// <remarks>
/// <para>
/// The tree is disposable. It is rebuilt from notation and node data whenever either changes, which is
/// what lets notation stay declarative: nothing in the model points at a cell, and a cell never
/// outlives the change that invalidated it. Anything that has to survive a rebuild — the caret, a
/// selection, a scroll anchor — therefore holds a <see cref="CellAddress"/>, not a cell.
/// </para>
/// <para>
/// Unlike the notation cells, the runtime tree is not split into inline and block types. A layout
/// container decides whether its content runs horizontally or vertically, so the same child cell can
/// be stacked in one place and run on a line in another without the tree having to be different.
/// What keeps notation from mixing them up is the notation type hierarchy, which is checked once at
/// registration; what keeps the runtime flexible is this one.
/// </para>
/// </remarks>
public abstract class EditorCell
{
    private static readonly EditorCell[] NoChildren = [];

    private protected EditorCell(CellAddress address) => Address = address;

    /// <summary>Where this cell sits in the model, and therefore what the caret can name it by.</summary>
    public CellAddress Address { get; }

    /// <summary>The cell this one is arranged inside, or <see langword="null"/> for a tree root.</summary>
    public EditorCell? Parent { get; internal set; }

    /// <summary>The cells arranged by this one, in document order.</summary>
    public virtual IReadOnlyList<EditorCell> Children => NoChildren;

    /// <summary>Whether this cell arranges nothing, which is the case for every cell that shows text.</summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>Whether the caret can stop on this cell, which is everything that shows something.</summary>
    public bool IsCaretTarget => IsLeaf;

    /// <summary>One line of text describing the cell, used by snapshots and test output.</summary>
    public sealed override string ToString() => Describe();

    /// <summary>What <see cref="ToString"/> prints; each cell says what it shows, not where it sits.</summary>
    internal abstract string Describe();
}
