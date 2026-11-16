namespace Projectional.Checks;

/// <summary>Turns a concept key into the words a finding and its repair are written with.</summary>
/// <remarks>
/// A check works from the declarations, so the only name it has for a concept is its key -
/// <c>Rules.Rule</c>. A message that says "this Rules.Rule is still called ..." reads like a stack trace,
/// so the last segment is used as an identifier ("Rule") and, made lower case, as prose ("this rule").
/// The same two words appear in the label the user clicks, which is why they live here rather than in one
/// check.
/// </remarks>
internal static class ConceptWords
{
    /// <summary>The concept key's last segment, as an identifier: <c>Rules.Rule</c> is <c>Rule</c>.</summary>
    internal static string Word(string conceptKey)
    {
        var last = conceptKey.LastIndexOf('.');
        var word = last < 0 ? conceptKey : conceptKey[(last + 1)..];

        return word.Length == 0 ? "Node" : word;
    }

    /// <summary>The concept as prose: <c>Rules.Rule</c> is <c>rule</c>.</summary>
    internal static string Describe(string conceptKey)
    {
        var word = Word(conceptKey);

        return word.Length == 0 ? "node" : char.ToLowerInvariant(word[0]) + word[1..];
    }
}
