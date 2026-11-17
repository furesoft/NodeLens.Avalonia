namespace Projectional.Editing;

/// <summary>
/// One entry of a completion list: a concept the slot at the caret could be filled with, or one of the
/// values a property slot allows.
/// </summary>
/// <remarks>
/// <para>
/// The item carries a payload string and nothing derived from it that the language would have to keep in
/// step, so the list cannot disagree with the model: <see cref="Label"/> is read off the payload, and the
/// payload is what accepting the item writes. <see cref="ConceptKey"/> is the payload, which is a concept
/// key for a <see cref="CompletionKind.Node"/> item and a symbol for a <see cref="CompletionKind.Value"/>
/// one - the kind says which, because only the list that produced the item knows.
/// </para>
/// <para>
/// There is no colour and no glyph here. What an item looks like is the host's decision, and a model
/// that carried a picture would have to be persisted with one - the same reason the syntax palette
/// lives above this layer.
/// </para>
/// </remarks>
public sealed class CompletionItem
{
    /// <summary>Creates an item for <paramref name="conceptKey"/>.</summary>
    /// <param name="conceptKey">The concept the item inserts, spelled the way the language declares it.</param>
    /// <param name="kind">What the item stands for, which is what a host chooses an icon by.</param>
    /// <param name="description">The concept's own sentence, or <see langword="null"/> when it declares none.</param>
    /// <exception cref="ArgumentException"><paramref name="conceptKey"/> is empty.</exception>
    public CompletionItem(string conceptKey, CompletionKind kind, string? description = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        ConceptKey = conceptKey;
        Kind = kind;
        Description = description;
    }

    /// <summary>
    /// What the language says about the concept, so a host can explain an entry it is highlighting.
    /// </summary>
    /// <remarks>
    /// Carried on the item rather than looked up by the presenter, because the editing layer is the layer
    /// that knows the registry: a list is built once and painted many times, and a painter that consulted
    /// the language would be a second place that knows what a proposal means.
    /// </remarks>
    public string? Description { get; }

    /// <summary>
    /// The payload of the item: a concept key for a node item, a symbol for a value item.
    /// </summary>
    /// <remarks>
    /// A symbol is a word the property type already spells, so it needs no key of its own - the value
    /// <em>is</em> the word, which is what makes accepting one a plain property write.
    /// </remarks>
    public string ConceptKey { get; }

    /// <summary>What the item stands for, which is what a host chooses an icon by.</summary>
    public CompletionKind Kind { get; }

    /// <summary>
    /// The text the item is shown and filtered by: the short name of a concept, the symbol itself for a
    /// value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A concept key is namespaced (<c>Expressions.IntLiteral</c>), and a list of full keys reads as
    /// noise. The short name is what the declaration itself calls the concept, so it needs no second
    /// name in the language.
    /// </para>
    /// <para>
    /// A value keeps its symbol verbatim, dots included. It is already the exact text the property stores,
    /// and shortening it the way a concept key is shortened would show a word the user cannot type.
    /// </para>
    /// </remarks>
    public string Label => field ??= Kind == CompletionKind.Value ? ConceptKey : LabelOf(ConceptKey);

    public override string ToString() => ConceptKey;

    /// <summary>Creates an item for one legal value of a property.</summary>
    /// <param name="symbol">The value as the property type spells it.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty.</exception>
    public static CompletionItem ForValue(string symbol) => new(symbol, CompletionKind.Value);

    /// <summary>Creates an item for one thing a reference may point at.</summary>
    /// <param name="name">The target's name, which is what the user sees and what the slot will show.</param>
    /// <param name="description">The target concept's own sentence, or <see langword="null"/>.</param>
    /// <remarks>
    /// The payload is the <em>name</em>, not the target's id, and that is deliberate: accepting re-resolves
    /// the name through the scope at the moment the key is pressed, exactly like a repair re-resolves its
    /// finding. A list that carried an id would link to a node the document may have moved on from, and a
    /// name that has since become ambiguous is refused instead of silently picking one of the two.
    /// </remarks>
    public static CompletionItem ForReference(string name, string? description = null) =>
        new(name, CompletionKind.Reference, description);

    private static string LabelOf(string conceptKey)
    {
        var dot = conceptKey.LastIndexOf('.');

        return dot >= 0 && dot < conceptKey.Length - 1 ? conceptKey[(dot + 1)..] : conceptKey;
    }
}
