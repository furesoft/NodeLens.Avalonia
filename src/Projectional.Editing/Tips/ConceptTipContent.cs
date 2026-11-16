namespace Projectional.Editing;

/// <summary>What a concept's tooltip says: which concept the node is, and the sentence the language wrote.</summary>
/// <remarks>
/// Two fields rather than one string, because the heading and the prose are decided differently - the key is
/// the language's own name for the concept and never changes, the sentence is documentation - and a renderer
/// may want to set them apart. The type exists so that "is there anything to show" is answered once, by
/// <see cref="ConceptTip.Of(Projectional.Core.Node)"/> returning <see langword="null"/>.
/// </remarks>
public sealed class ConceptTipContent
{
    /// <summary>Describes one concept.</summary>
    /// <param name="conceptKey">The concept's key, which is what the heading shows.</param>
    /// <param name="description">The sentence the language declared about the concept.</param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is empty.</exception>
    public ConceptTipContent(string conceptKey, string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentException.ThrowIfNullOrEmpty(description);

        ConceptKey = conceptKey;
        Description = description;
    }

    /// <summary>The concept's key, which the notation never shows.</summary>
    /// <remarks>
    /// The key rather than a friendly label: it is what a language author greps for, and a label would have
    /// to be declared somewhere before it could be shown.
    /// </remarks>
    public string ConceptKey { get; }

    /// <summary>What the language says about the concept.</summary>
    public string Description { get; }

    /// <inheritdoc />
    public override string ToString() => ConceptKey;
}
