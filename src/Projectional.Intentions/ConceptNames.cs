namespace Projectional.Intentions;

/// <summary>The name a concept is called by inside an offer's label.</summary>
/// <remarks>
/// A concept key is stable and never translated (<c>Expressions.Parenthesized</c>), so a label built from it
/// cannot drift from the thing it names. Only the last segment is used: the prefix names the language, which is
/// where the user already is, and <c>Wrap in "Expressions.Parenthesized"</c> would read as a file path.
/// </remarks>
internal static class ConceptNames
{
    /// <summary>The segment of <paramref name="conceptKey"/> after its last dot.</summary>
    /// <param name="conceptKey">A concept key, such as <c>Expressions.Parenthesized</c>.</param>
    internal static string Of(string conceptKey)
    {
        var dot = conceptKey.LastIndexOf('.');

        return dot < 0 ? conceptKey : conceptKey[(dot + 1)..];
    }
}
