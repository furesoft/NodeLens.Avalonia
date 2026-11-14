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
}
