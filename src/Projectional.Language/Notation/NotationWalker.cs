namespace Projectional.Language;

/// <summary>
/// Walks notation trees. Kept in one place so that every consumer — validation, projection, tooling —
/// sees the cells in the same order.
/// </summary>
internal static class NotationWalker
{
    /// <summary>The children of <paramref name="cell"/> in document order, or none for a leaf.</summary>
    public static IReadOnlyList<NotationCell> ChildrenOf(NotationCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        return cell switch
        {
            LineNotationCell line => line.Content,
            SequenceNotationCell sequence => sequence.Content,
            IndentNotationCell indent => indent.Content,
            HorizontalNotationCell horizontal => horizontal.Content,
            ConditionalNotationCell conditional => conditional.Content,
            WhenFilledNotationCell whenFilled => whenFilled.Content,
            WhenFilledBlockNotationCell whenFilledBlock => whenFilledBlock.Content,
            ConditionalBlockNotationCell conditionalBlock => conditionalBlock.Content,
            _ => [],
        };
    }

    /// <summary>Every cell below <paramref name="cell"/>, depth first, in document order.</summary>
    public static IEnumerable<NotationCell> Descendants(NotationCell cell)
    {
        foreach (var child in ChildrenOf(cell))
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
