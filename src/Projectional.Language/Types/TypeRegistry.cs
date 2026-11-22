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
        ComparisonRelation comparisons,
        IReadOnlyList<TypeReplacement> replacements)
    {
        _rules = rules;
        Subtypes = subtypes;
        Comparisons = comparisons;
        Replacements = replacements;
    }

    /// <summary>A registry that declares nothing, so every node is <see cref="UnknownType"/>.</summary>
    public static TypeRegistry Empty { get; } = new([], SubtypeRelation.Primitives, ComparisonRelation.Empty, []);

    /// <summary>
    /// The subtype edges the language declared, which a consumer solving equations hands to a
    /// <see cref="TypeSolver"/> or <see cref="TypeLattice"/>.
    /// </summary>
    /// <remarks>
    /// A registry owns the whole of a language's types aspect: the rules that answer what a node's type is, the edges
    /// that say which types may stand where another is required, the pairs it declares interchangeable, and the
    /// replacements that say what a claim about two compound types really means. Keeping them together is what lets a
    /// consumer solve with the language's own relations instead of only the framework's primitives - they are
    /// otherwise reachable only by a C# caller that built them by hand.
    /// </remarks>
    public SubtypeRelation Subtypes { get; }

    /// <summary>
    /// The replacements the language declared, which a consumer hands to a <see cref="TypeSolver"/> beside
    /// <see cref="Subtypes"/>.
    /// </summary>
    /// <remarks>
    /// A replacement is what a claim about two compound types really means, where the edges say which types may stand
    /// where another is required. They sit on the registry for the same reason the edges do - so that a consumer
    /// solving a rule table is given the language's own rules rather than only the framework's.
    /// </remarks>
    public IReadOnlyList<TypeReplacement> Replacements { get; }

    /// <summary>
    /// The types the language declared interchangeable, which a consumer asks beside <see cref="Subtypes"/>.
    /// </summary>
    /// <remarks>
    /// The one that answers "may each of these be used for the other" without claiming either is below the other. It
    /// is kept apart from <see cref="Subtypes"/> on purpose: two edges, one each way, would say the same about
    /// interchange while also collapsing the two types in <see cref="TypeLattice"/>, and a language may mean the
    /// first without the second.
    /// </remarks>
    public ComparisonRelation Comparisons { get; }

    /// <summary>The same registry, carrying <paramref name="subtypes"/> as its declared edges.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="subtypes"/> is <see langword="null"/>.</exception>
    public TypeRegistry WithSubtypes(SubtypeRelation subtypes)
    {
        ArgumentNullException.ThrowIfNull(subtypes);

        return ReferenceEquals(subtypes, Subtypes) ? this : new TypeRegistry(_rules, subtypes, Comparisons, Replacements);
    }

    /// <summary>The same registry, carrying <paramref name="comparisons"/> as its declared comparisons.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="comparisons"/> is <see langword="null"/>.</exception>
    public TypeRegistry WithComparisons(ComparisonRelation comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);

        return ReferenceEquals(comparisons, Comparisons)
            ? this
            : new TypeRegistry(_rules, Subtypes, comparisons, Replacements);
    }

    /// <summary>The same registry, carrying <paramref name="replacements"/> as its declared replacements.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="replacements"/> is <see langword="null"/>.</exception>
    public TypeRegistry WithReplacements(IReadOnlyList<TypeReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);

        return ReferenceEquals(replacements, Replacements)
            ? this
            : new TypeRegistry(_rules, Subtypes, Comparisons, replacements);
    }

    /// <summary>How many concepts declare a rule.</summary>
    public int Count => _rules.Count;

    /// <summary>The rule each concept declares, keyed by concept, in the order they were added.</summary>
    /// <remarks>
    /// Readable for the same reason <see cref="TemplateRegistry.Templates"/> is: an export reads the registry the other
    /// way round, writing the rules out as the code that declares them. The dictionary is the registry's own, so a
    /// caller must not mutate it - there is nothing to mutate, because <see cref="Add"/> answers a new registry.
    /// </remarks>
    public IReadOnlyDictionary<string, ITypeRule> Rules => _rules;

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

        return new TypeRegistry(rules, Subtypes, Comparisons, Replacements);
    }

    /// <summary>
    /// The union of this registry and <paramref name="other"/>: every rule, edge, pair and replacement of both,
    /// with this registry's statement winning for a concept both answer for.
    /// </summary>
    /// <param name="other">The registry to merge in - the base an extending language inherited.</param>
    /// <remarks>
    /// <para>
    /// One concept has one type rule, so a union cannot hold two: the nearer registry's rule replaces the farther
    /// one's. "Nearer" is the caller's order, and <see cref="LanguageProject"/> passes its own registry first, then
    /// its bases nearest first.
    /// </para>
    /// <para>
    /// The relations and the replacements are additive: a base's declared edge or interchange is a fact about its
    /// types that the extending language inherits rather than restates.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public TypeRegistry With(TypeRegistry other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var rules = new Dictionary<string, ITypeRule>(other._rules, StringComparer.Ordinal);

        foreach (var (conceptKey, rule) in _rules)
        {
            rules[conceptKey] = rule;
        }

        return new TypeRegistry(
            rules,
            Subtypes.With(other.Subtypes),
            Comparisons.With(other.Comparisons),
            [.. Replacements, .. other.Replacements]);
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
