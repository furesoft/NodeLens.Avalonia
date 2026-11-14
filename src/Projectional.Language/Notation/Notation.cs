namespace Projectional.Language;

/// <summary>
/// How one concept is drawn: a single block cell that the projection engine expands for every node
/// of the concept.
/// </summary>
public sealed class Notation
{
    public Notation(string conceptKey, BlockNotationCell root)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(root);
        ConceptKey = conceptKey;
        Root = root;
    }

    public string ConceptKey { get; }

    public BlockNotationCell Root { get; }

    /// <summary>Every cell of the notation, the root included, in document order.</summary>
    public IEnumerable<NotationCell> SelfAndDescendants()
    {
        yield return Root;

        foreach (var cell in NotationWalker.Descendants(Root))
        {
            yield return cell;
        }
    }

    public override string ToString() => $"{ConceptKey} notation";
}
