namespace Projectional.Editing;

/// <summary>
/// What a tooltip says: a heading, and the sentence the language wrote about the thing it names.
/// </summary>
/// <remarks>
/// <para>
/// Two fields rather than one string, because the heading and the prose are decided differently - a concept key
/// and a slot name are the language's own names for things and never change, while the sentence is
/// documentation - and a renderer may want to set them apart. The type is shared by the two tips the editor
/// shows over text, because a box with a heading and a paragraph is one shape: <see cref="ConceptTip"/> fills it
/// for a node, <see cref="PropertyTip"/> for a slot of that node.
/// </para>
/// <para>
/// The type exists so that "is there anything to show" is answered once, by the factory returning
/// <see langword="null"/>: a box that repeated the heading with nothing under it would teach the user that
/// hovering is pointless.
/// </para>
/// </remarks>
public sealed class TipContent
{
    /// <summary>Describes one thing a language documented.</summary>
    /// <param name="heading">The name shown first: a concept key or a slot name.</param>
    /// <param name="description">The sentence the language declared.</param>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is empty or, for the sentence, blank.</exception>
    public TipContent(string heading, string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Heading = heading;
        Description = description;
    }

    /// <summary>The name the box shows first: a concept key, or the name of a slot.</summary>
    /// <remarks>
    /// The key rather than a friendly label: it is what a language author greps for, and a label would have to be
    /// declared somewhere before it could be shown.
    /// </remarks>
    public string Heading { get; }

    /// <summary>What the language says about it.</summary>
    public string Description { get; }

    /// <inheritdoc />
    public override string ToString() => Heading;
}