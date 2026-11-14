namespace Projectional.Projection;

/// <summary>
/// Walks a cell tree in document order.
/// </summary>
/// <remarks>
/// Document order is the only order that matters to the editor: the caret moves through it, layout
/// measures in it, and hit testing and rendering assume it. It lives here as one place rather than as
/// a <c>foreach</c> that each caller writes slightly differently, because a walk that visits siblings
/// in a different order than the renderer draws them is a bug that only shows up as a caret that
/// jumps.
/// </remarks>
internal static class EditorTreeWalker
{
    /// <summary>The cell and every cell below it, in document order.</summary>
    public static IEnumerable<EditorCell> SelfAndDescendants(EditorCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        yield return cell;

        foreach (var child in cell.Children)
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
