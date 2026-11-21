using Projectional.Core;
using Projectional.Language;

namespace Projectional.TypingRules;

/// <summary>Turns written typing rules into the <see cref="ITypingRules"/> a session installs.</summary>
/// <remarks>
/// <para>
/// This is the half that makes the declared path equal rather than weaker (ADR 0014): every rule becomes an entry of
/// the same <see cref="TypingRuleRegistry"/> a C# language fills with <c>Add</c>, so a declared rule and a written one
/// are indistinguishable to the editor - including which of two claimants wins, which the registry decides by the
/// order the language declared its concepts.
/// </para>
/// <para>
/// It checks what a compiler would have checked - the concept exists, the claim is one of the four symbols, the text a
/// claim needs is there and the text a claim does not need is not, the property exists on that concept and is a
/// property rather than a child or a reference, and no two rules claim the same concept. <b>Two rules for one concept
/// are refused rather than merged</b>, for the reason the registry's own <c>Add</c> refuses them: one concept is one
/// answer to "what did the user mean", and two rules would leave the language unable to say which of its properties
/// the digit belongs in.
/// </para>
/// </remarks>
public static class TypingLowering
{
    /// <summary>The rules a language's typing documents declare.</summary>
    /// <param name="documents">
    /// The roots of those documents: one <see cref="TypingConceptKeys.Rule"/> per document, in file order.
    /// </param>
    /// <param name="concepts">The concepts those rules may name, which is the language's own registry.</param>
    /// <returns>The registry the editor asks, or the empty one when there are no documents.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A document cannot be lowered.</exception>
    public static TypingRuleRegistry CreateRules(IEnumerable<Node> documents, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(concepts);

        var rules = TypingRuleRegistry.Empty;
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in documents)
        {
            if (rule.Concept.Key != TypingConceptKeys.Rule)
            {
                throw new ConceptDefinitionException(
                    $"'{rule.Concept.Key}' is not a typing rule; a typing document holds one '{TypingConceptKeys.Rule}'.");
            }

            var conceptKey = Required(rule, TypingLanguage.ConceptKeySlot, "A typing rule has to name the concept the text creates.");
            var claims = Required(rule, TypingLanguage.ClaimsSlot, $"A typing rule of '{conceptKey}' has to say how the text is recognised.");
            var text = Optional(rule, TypingLanguage.TextSlot);
            var property = Optional(rule, TypingLanguage.PropertySlotSlot);

            if (!concepts.TryGet(conceptKey, out var concept))
            {
                throw new ConceptDefinitionException(
                    $"A typing rule names '{conceptKey}', which is not a concept of this language.");
            }

            if (property is not null && !concept.Slots.OfType<PropertySlot>().Any(slot => slot.Name == property))
            {
                throw new ConceptDefinitionException(
                    $"'{conceptKey}' declares no property named '{property}'; a typing rule writes the text into a property, or into nothing.");
            }

            if (!claimed.Add(conceptKey))
            {
                throw new ConceptDefinitionException(
                    $"Two typing rules claim '{conceptKey}'; one concept is one answer to what the text creates, so state it once.");
            }

            rules = rules.Add(conceptKey, ClaimsOf(conceptKey, claims, text), property);
        }

        return rules;
    }

    /// <summary>The predicate a claim symbol stands for, checked against the text the claim needs.</summary>
    /// <remarks>
    /// Each branch is the C# twin verbatim - <c>char.IsAsciiDigit</c>, the name shape <c>ExpressionsLanguage</c> writes,
    /// <c>StartsWith</c> and an equality against each word - so that a declared rule and a written one answer the same
    /// text the same way. A claim that needs a text without one, or carries a text it never reads, is refused rather
    /// than ignored: a rule whose words are not read is a rule the author got wrong.
    /// </remarks>
    private static Func<string, bool> ClaimsOf(string conceptKey, string claims, string? text)
    {
        switch (claims)
        {
            case TypingClaims.Digits:
                RefuseText(conceptKey, claims, text);

                return Digits;

            case TypingClaims.Name:
                RefuseText(conceptKey, claims, text);

                return IsName;

            case TypingClaims.Prefix:
                return StartsWith(conceptKey, text);

            case TypingClaims.AnyOf:
                return AnyOf(conceptKey, text);

            default:
                throw new ConceptDefinitionException(
                    $"'{claims}' is not a claim; a typing rule claims {TypingClaims.Digits}, {TypingClaims.Name}, "
                    + $"'{TypingClaims.Prefix}' or '{TypingClaims.AnyOf}'.");
        }
    }

    private static bool Digits(string text) => text.All(char.IsAsciiDigit);

    private static bool IsName(string text) =>
        text.Length > 0
        && (char.IsAsciiLetter(text[0]) || text[0] == '_')
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static Func<string, bool> StartsWith(string conceptKey, string? prefix) =>
        string.IsNullOrEmpty(prefix)
            ? throw new ConceptDefinitionException(
                $"The typing rule of '{conceptKey}' starts with nothing, which would claim every text; name the prefix.")
            : text => text.StartsWith(prefix, StringComparison.Ordinal);

    private static Func<string, bool> AnyOf(string conceptKey, string? text)
    {
        var words = (text ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length == 0
            ? throw new ConceptDefinitionException(
                $"The typing rule of '{conceptKey}' names no word, so it would claim nothing; write the words separated by spaces.")
            : text => words.Contains(text, StringComparer.Ordinal);
    }

    /// <summary>Refuses a text on a claim that reads none, so a rule cannot carry words nothing looks at.</summary>
    private static void RefuseText(string conceptKey, string claims, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            throw new ConceptDefinitionException(
                $"The typing rule of '{conceptKey}' claims '{claims}', which reads no text; leave the text empty.");
        }
    }

    private static string Required(Node rule, string slot, string message) =>
        Optional(rule, slot) ?? throw new ConceptDefinitionException(message);

    private static string? Optional(Node rule, string slot) =>
        rule.GetOrNull(slot) is { Length: > 0 } value ? value : null;
}
