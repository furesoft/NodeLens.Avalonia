using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// The evaluation rules a language declares, keyed by concept, with inheritance and nothing behind them.
/// </summary>
/// <remarks>
/// <para>
/// A concept's rule is its own or, failing that, the nearest super concept's - the same lookup notation,
/// triggers, scope providers, type rules and constraints use, so a language has one answer to "which
/// declaration applies". Unlike a type, a concept with no rule has no answer at all: there is no
/// <c>UnknownValue</c> to fall back on, because "nobody said what this means" is a reason to stop rather than
/// a value to hand back.
/// </para>
/// <para>
/// The registry is immutable and <see cref="Add"/> answers a new one, so a language can be composed without
/// mutating a registry another session is using - the same shape as <see cref="TypeRegistry"/>.
/// </para>
/// </remarks>
public sealed class EvaluationRegistry
{
    private readonly Dictionary<string, IEvaluationRule> _rules;

    private EvaluationRegistry(Dictionary<string, IEvaluationRule> rules) => _rules = rules;

    /// <summary>A registry that declares nothing, so no node can be evaluated.</summary>
    public static EvaluationRegistry Empty { get; } = new([]);

    /// <summary>How many concepts declare a rule.</summary>
    public int Count => _rules.Count;

    /// <summary>The same registry with <paramref name="rule"/> answering for <paramref name="conceptKey"/>.</summary>
    /// <param name="conceptKey">The concept the rule is about.</param>
    /// <param name="rule">What a node of that concept evaluates to.</param>
    /// <exception cref="ArgumentException">The concept key is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public EvaluationRegistry Add(string conceptKey, IEvaluationRule rule)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(rule);

        var rules = new Dictionary<string, IEvaluationRule>(_rules, StringComparer.Ordinal)
        {
            [conceptKey] = rule,
        };

        return new EvaluationRegistry(rules);
    }

    /// <summary>The rules of both registries, with this one's rule winning where both answer for a concept.</summary>
    /// <param name="other">The registry to add behind this one, such as a base language's.</param>
    /// <returns>A registry that answers with this one's rules first and <paramref name="other"/>'s for what is left.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The same shape <c>TypeRegistry.With</c> has, and for the same reason: a language that extends another inherits its
    /// meanings unless it states one of its own, which is what "behaviour is inherited through extends" means
    /// ([ADR 0027](../../../docs/adr/0027-behaviour-is-inherited-through-extends.md)).
    /// </remarks>
    public EvaluationRegistry With(EvaluationRegistry other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var rules = new Dictionary<string, IEvaluationRule>(other._rules, StringComparer.Ordinal);

        foreach (var (conceptKey, rule) in _rules)
        {
            rules[conceptKey] = rule;
        }

        return new EvaluationRegistry(rules);
    }

    /// <summary>The rule that answers for <paramref name="concept"/>, or <see langword="null"/>.</summary>
    /// <param name="concept">The concept to ask about.</param>
    public IEvaluationRule? RuleFor(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        return _rules.Count == 0 ? null : Find(concept, []);
    }

    private IEvaluationRule? Find(Concept concept, HashSet<string> seen)
    {
        if (!seen.Add(concept.Key))
        {
            return null;
        }

        if (_rules.TryGetValue(concept.Key, out var rule))
        {
            return rule;
        }

        foreach (var super in concept.SuperConcepts)
        {
            if (Find(super, seen) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    public override string ToString() => $"{_rules.Count} rule(s)";
}
