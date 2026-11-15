namespace Projectional.Language;

/// <summary>
/// How one concept is drawn: a single block cell that the projection engine expands for every node
/// of the concept.
/// </summary>
/// <remarks>
/// A concept may have more than one of these, distinguished by <see cref="Variant"/>. The notation
/// itself is data and does not say which of them a view gets; that is the job of an
/// <see cref="INotationChoice"/> the projection consults, which is why a notation can be added,
/// listed and validated without knowing who will read it.
/// </remarks>
public sealed class Notation
{
    /// <summary>The variant a view gets when it expresses no opinion.</summary>
    public const string DefaultVariant = "default";

    /// <summary>Declares the default variant of <paramref name="conceptKey"/>.</summary>
    public Notation(string conceptKey, BlockNotationCell root)
        : this(conceptKey, DefaultVariant, root)
    {
    }

    /// <summary>Declares one variant of <paramref name="conceptKey"/>.</summary>
    public Notation(string conceptKey, string variant, BlockNotationCell root)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentException.ThrowIfNullOrEmpty(variant);
        ArgumentNullException.ThrowIfNull(root);

        ConceptKey = conceptKey;
        Variant = variant;
        Root = root;
    }

    public string ConceptKey { get; }

    /// <summary>
    /// Which of the concept's drawings this is, e.g. <c>"default"</c> or <c>"table"</c>. Only ever
    /// compared ordinally: it is a name a language author writes and a host configures, never
    /// something displayed and translated.
    /// </summary>
    public string Variant { get; }

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

    public override string ToString() => $"{ConceptKey} notation ({Variant})";
}
