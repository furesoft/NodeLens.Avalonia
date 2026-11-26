namespace Projectional.Language;

/// <summary>
/// A cell that occupies whole lines: a line, an indented block, or a child list. Blocks nest into
/// blocks, never into a <see cref="LineNotationCell"/>.
/// </summary>
public abstract class BlockNotationCell : NotationCell
{
    private protected BlockNotationCell()
    {
    }

    /// <summary>
    /// How the block is decorated: the room around its content, a line around it and what fills it.
    /// </summary>
    /// <remarks>
    /// A property on the base rather than a cell type of its own, because a decoration is orthogonal to what a block
    /// arranges - a sequence, a table and a child slot can all be boxed, and a notation must not have to choose
    /// between "a table" and "a bordered table". <see langword="null"/> draws the block plainly, which is what every
    /// notation written before this property existed means.
    /// </remarks>
    public CellPaint? Paint { get; init; }
}
