namespace Projectional.Editing;

/// <summary>What to look for, and whether case matters.</summary>
/// <param name="Text">The text to find; empty finds nothing.</param>
/// <param name="MatchCase">Whether <paramref name="Text"/> must match letter for letter.</param>
/// <remarks>
/// <para>
/// A query is a value, not a running search: nothing is cached, so a query held by a UI is still correct after the
/// document has changed underneath it - the next search simply finds what is there now.
/// </para>
/// <para>
/// Matching is ordinal, not culture-aware. A projectional document is code-like: comparing under a culture would make
/// "i" match "i" in Turkish and not in German, and a search whose result depends on the machine's locale is one a
/// user cannot reason about.
/// </para>
/// </remarks>
public sealed record FindQuery(string Text, bool MatchCase = false)
{
    /// <summary>Whether this query can match anything at all.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Text);

    /// <summary>How the text is compared.</summary>
    internal StringComparison Comparison =>
        MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}