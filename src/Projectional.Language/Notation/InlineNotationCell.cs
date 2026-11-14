namespace Projectional.Language;

/// <summary>
/// A cell that is placed inside a <see cref="LineNotationCell"/> and shares its line with the cells
/// around it. Inline cells are the only thing a line may contain.
/// </summary>
public abstract class InlineNotationCell : NotationCell
{
    private protected InlineNotationCell()
    {
    }
}
