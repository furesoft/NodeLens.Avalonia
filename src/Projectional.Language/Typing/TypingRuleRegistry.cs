using System.Collections.Immutable;

namespace Projectional.Language;

/// <summary>
/// A <see cref="ITypingRules"/> that keeps one rule per concept, as an immutable value.
/// </summary>
/// <remarks>
/// <para>
/// Immutable because a language declares its rules once and hands the same registry to every session it
/// opens: a registry that could grow would let one document's language leak into another's.
/// <see cref="Add"/> returns a new registry, so declaring a language reads as one expression and the
/// order the calls are written in is the order the rules are kept in.
/// </para>
/// <para>
/// The order the rules are added in does not decide anything: the editor asks the concepts a slot can
/// hold in the order the language declared them, which is the same convention
/// <see cref="Projectional.Core.ConceptRegistry.DefaultInstantiableFor"/> uses to pick a default concept.
/// Where two rules would both claim a text, the earlier-declared concept therefore wins.
/// </para>
/// </remarks>
public sealed class TypingRuleRegistry : ITypingRules
{
    private readonly ImmutableDictionary<string, TypingRule> _byConcept;

    private TypingRuleRegistry(ImmutableDictionary<string, TypingRule> byConcept) => _byConcept = byConcept;

    /// <summary>A registry that claims nothing, so typing into an empty slot leaves the model alone.</summary>
    public static TypingRuleRegistry Empty { get; } =
        new(ImmutableDictionary.Create<string, TypingRule>(StringComparer.Ordinal));

    /// <summary>This registry, with <paramref name="conceptKey"/> claiming text that <paramref name="claims"/> accepts.</summary>
    /// <param name="conceptKey">The concept to create when the text is claimed.</param>
    /// <param name="claims">Whether a burst of typed text belongs to that concept.</param>
    /// <param name="propertySlot">
    /// The property of the concept the text is written into, or <see langword="null"/> when the concept
    /// has no property to put it in.
    /// </param>
    /// <returns>A new registry; this one is unchanged.</returns>
    /// <exception cref="ArgumentException"><paramref name="conceptKey"/> already has a rule.</exception>
    /// <remarks>
    /// One rule per concept, because a concept is one answer to the question: two rules for one concept
    /// would be a language that cannot say which of its own properties a digit belongs in.
    /// </remarks>
    public TypingRuleRegistry Add(string conceptKey, Func<string, bool> claims, string? propertySlot = null)
    {
        var rule = new TypingRule(conceptKey, claims, propertySlot);

        if (_byConcept.ContainsKey(conceptKey))
        {
            throw new ArgumentException(
                $"Concept '{conceptKey}' already has a typing rule.",
                nameof(conceptKey));
        }

        return new TypingRuleRegistry(_byConcept.Add(conceptKey, rule));
    }

    /// <inheritdoc/>
    public TypingRule? ClaimFor(string conceptKey, string text) =>
        conceptKey is not null
        && _byConcept.TryGetValue(conceptKey, out var rule)
        && rule.Claims(text)
            ? rule
            : null;

    public override string ToString() =>
        _byConcept.IsEmpty
            ? "no typing rules"
            : string.Join(", ", _byConcept.Keys.Order(StringComparer.Ordinal));
}
