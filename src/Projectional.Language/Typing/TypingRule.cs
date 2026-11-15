namespace Projectional.Language;

/// <summary>
/// One concept's answer to "the user is typing into an empty slot: is that me?".
/// </summary>
/// <remarks>
/// <para>
/// A slot declared over an abstract concept names a set rather than a node, so when the user starts
/// typing there is nothing in the model that says which member of the set they mean. Deriving it from
/// the concepts themselves does not work: <c>abc</c> and <c>"abc"</c> are both plausible values of a
/// string property, and only the language knows that the first is a name and the second a literal. So
/// the language states it, once per concept, and the editor asks.
/// </para>
/// <para>
/// <see cref="PropertySlot"/> is stated for the same reason. Several properties of one concept can
/// accept the same text - a name and a string literal are both strings - so which one the typed text
/// belongs in is a language decision, not something a search over the concept can decide.
/// </para>
/// </remarks>
public sealed class TypingRule
{
    private readonly Func<string, bool> _claims;

    /// <summary>Declares that <paramref name="conceptKey"/> claims text matching <paramref name="claims"/>.</summary>
    /// <param name="conceptKey">The concept to create when this rule claims the text.</param>
    /// <param name="claims">Whether a burst of typed text belongs to this concept.</param>
    /// <param name="propertySlot">
    /// The property of <paramref name="conceptKey"/> the text is written into, or <see langword="null"/>
    /// when the concept has no property to put it in and the text only selects the concept.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="conceptKey"/> or <paramref name="propertySlot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="claims"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The predicate sees the whole burst of text the host typed at once, not one character at a time,
    /// which is what lets a concept claim a word such as <c>true</c> that no single keystroke announces.
    /// </remarks>
    public TypingRule(string conceptKey, Func<string, bool> claims, string? propertySlot = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(claims);

        if (propertySlot is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(propertySlot);
        }

        ConceptKey = conceptKey;
        PropertySlot = propertySlot;
        _claims = claims;
    }

    /// <summary>The concept to create when this rule claims the text.</summary>
    public string ConceptKey { get; }

    /// <summary>
    /// The property of <see cref="ConceptKey"/> the typed text belongs in, or <see langword="null"/> when
    /// the concept has no property to put it in and the text only selects the concept.
    /// </summary>
    /// <remarks>
    /// The property is filled only if the concept declares it and its type accepts the text. A rule that
    /// names a slot the concept does not have, or a text its type rejects, therefore still creates the
    /// node and merely drops the text: a keystroke must not be able to throw, and a language author sees
    /// the mistake immediately as "the right concept appeared, unfilled".
    /// </remarks>
    public string? PropertySlot { get; }

    /// <summary>Whether a burst of typed text belongs to this concept.</summary>
    /// <param name="text">The characters the host typed.</param>
    public bool Claims(string text) => _claims(text);

    public override string ToString() =>
        PropertySlot is null ? ConceptKey : $"{ConceptKey}.{PropertySlot}";
}
