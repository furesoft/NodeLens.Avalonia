namespace Projectional.Editing;

/// <summary>
/// One entry of a completion list: a concept the slot at the caret could be filled with.
/// </summary>
/// <remarks>
/// <para>
/// The item carries the concept key and nothing derived from it that the language would have to keep in
/// step, so the list cannot disagree with the model: <see cref="Label"/> is read off the key, and the
/// key is what filling the slot asks the model for.
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
    /// <exception cref="ArgumentException"><paramref name="conceptKey"/> is empty.</exception>
    public CompletionItem(string conceptKey, CompletionKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        ConceptKey = conceptKey;
        Kind = kind;
    }

    /// <summary>The concept this item inserts, spelled the way the language declares it.</summary>
    public string ConceptKey { get; }

    /// <summary>What the item stands for, which is what a host chooses an icon by.</summary>
    public CompletionKind Kind { get; }

    /// <summary>
    /// The last segment of <see cref="ConceptKey"/>, which is the short name the user types against.
    /// </summary>
    /// <remarks>
    /// A concept key is namespaced (<c>Expressions.IntLiteral</c>), and a list of full keys reads as
    /// noise. The short name is what the declaration itself calls the concept, so it needs no second
    /// name in the language.
    /// </remarks>
    public string Label => field ??= LabelOf(ConceptKey);

    public override string ToString() => ConceptKey;

    private static string LabelOf(string conceptKey)
    {
        var dot = conceptKey.LastIndexOf('.');

        return dot >= 0 && dot < conceptKey.Length - 1 ? conceptKey[(dot + 1)..] : conceptKey;
    }
}
