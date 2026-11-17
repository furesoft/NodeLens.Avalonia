using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// The type rules a language declares, keyed by concept, with <see cref="UnknownType"/> behind them.
/// </summary>
/// <remarks>
/// <para>
/// A concept's rule is its own or, failing that, the nearest super concept's - the same lookup notation,
/// triggers, scope providers and constraints use, so a language has one answer to "which declaration
/// applies". A concept that declares nothing is <see cref="UnknownType"/>: nobody said, so nothing is
/// known, and every consumer is told that rather than being handed a guess.
/// </para>
/// <para>
/// The registry is immutable and <see cref="Add"/> answers a new one, so a language can be composed
/// without mutating a registry another session is using.
/// </para>
/// </remarks>
public sealed class TypeRegistry
{
    private readonly Dictionary<string, ITypeRule> _rules;

    private TypeRegistry(Dictionary<string, ITypeRule> rules) => _rules = rules;

    /// <summary>A registry that declares nothing, so every node is <see cref="UnknownType"/>.</summary>
    public static TypeRegistry Empty { get; } = new([]);

    /// <summary>How many concepts declare a rule.</summary>
    public int Count => _rules.Count;

    /// <summary>The same registry with <paramref name="rule"/> answering for <paramref name="conceptKey"/>.</summary>
    /// <exception cref="ArgumentException">The concept key is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public TypeRegistry Add(string conceptKey, ITypeRule rule)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(rule);

        var rules = new Dictionary<string, ITypeRule>(_rules, StringComparer.Ordinal)
        {
            [conceptKey] = rule,
        };

        return new TypeRegistry(rules);
    }

    /// <summary>The rule that answers for <paramref name="concept"/>, or <see langword="null"/>.</summary>
    public ITypeRule? RuleFor(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        return _rules.Count == 0 ? null : Find(concept, []);
    }

    /// <summary>What <paramref name="node"/> evaluates to, or <see cref="UnknownType"/> when nothing says.</summary>
    public IType TypeOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return RuleFor(node.Concept) is { } rule
            ? rule.TypeFor(new TypeContext(node, this))
            : UnknownType.Instance;
    }

    private ITypeRule? Find(Concept concept, HashSet<string> seen)
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
