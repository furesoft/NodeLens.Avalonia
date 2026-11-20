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

    private TypeRegistry(
        Dictionary<string, ITypeRule> rules,
        SubtypeRelation subtypes,
        IReadOnlyList<TypeReplacement> replacements)
    {
        _rules = rules;
        Subtypes = subtypes;
        Replacements = replacements;
    }

    /// <summary>A registry that declares nothing, so every node is <see cref="UnknownType"/>.</summary>
    public static TypeRegistry Empty { get; } = new([], SubtypeRelation.Primitives, []);

    /// <summary>
    /// The subtype edges the language declared, which a consumer solving equations hands to a
    /// <see cref="TypeSolver"/> or <see cref="TypeLattice"/>.
    /// </summary>
    /// <remarks>
    /// A registry owns both halves of a language's types aspect: the rules that answer what a node's type is,
    /// and the edges that say which types may stand where another is required. Keeping them together is what
    /// lets a consumer solve with the language's own relation instead of only the framework's primitives - the
    /// relation is otherwise reachable only by a C# caller that built the edges by hand.
    /// </remarks>
    public SubtypeRelation Subtypes { get; }

    /// <summary>
    /// The replacements the language declared, which a consumer hands to a <see cref="TypeSolver"/> beside
    /// <see cref="Subtypes"/>.
    /// </summary>
    /// <remarks>
    /// A replacement is the third half of a language's types aspect: the edges say which types may stand where
    /// another is required, and the replacements say what a claim about two compound types really means. They sit
    /// on the registry for the same reason the edges do - so that a consumer solving a rule table is given the
    /// language's own rules rather than only the framework's.
    /// </remarks>
    public IReadOnlyList<TypeReplacement> Replacements { get; }

    /// <summary>The same registry, carrying <paramref name="subtypes"/> as its declared edges.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="subtypes"/> is <see langword="null"/>.</exception>
    public TypeRegistry WithSubtypes(SubtypeRelation subtypes)
    {
        ArgumentNullException.ThrowIfNull(subtypes);

        return ReferenceEquals(subtypes, Subtypes) ? this : new TypeRegistry(_rules, subtypes, Replacements);
    }

    /// <summary>The same registry, carrying <paramref name="replacements"/> as its declared replacements.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="replacements"/> is <see langword="null"/>.</exception>
    public TypeRegistry WithReplacements(IReadOnlyList<TypeReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);

        return ReferenceEquals(replacements, Replacements)
            ? this
            : new TypeRegistry(_rules, Subtypes, replacements);
    }

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

        return new TypeRegistry(rules, Subtypes, Replacements);
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

    /// <summary>
    /// The relations the rule for <paramref name="node"/> states, which a <see cref="TypeSolver"/> satisfies.
    /// </summary>
    /// <remarks>
    /// A rule answers two separate questions - what type this node is, and what must hold for the types around
    /// it - and this is the second one. It is a method on the registry rather than on the node because the
    /// equations only exist while a rule is being evaluated against a node, and the registry is what owns both.
    /// </remarks>
    /// <param name="node">The node whose rule states the relations.</param>
    /// <returns>The relations, in the order the rule states them; empty when no rule answers for the node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public IEnumerable<TypeEquation> EquationsFor(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return RuleFor(node.Concept) is { } rule
            ? rule.EquationsFor(new TypeContext(node, this))
            : [];
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
