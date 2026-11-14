namespace Projectional.Language;

/// <summary>
/// One element of a concept's notation. Notation is data, never code: the projection engine walks
/// these cells and the editor tree is what it builds from them.
/// </summary>
/// <remarks>
/// Cells come in two shapes, and the split is expressed in the type hierarchy so that a notation
/// cannot mix them up. <see cref="InlineNotationCell"/> runs horizontally inside a line;
/// <see cref="BlockNotationCell"/> occupies lines of its own.
/// </remarks>
public abstract class NotationCell
{
    private protected NotationCell()
    {
    }
}
