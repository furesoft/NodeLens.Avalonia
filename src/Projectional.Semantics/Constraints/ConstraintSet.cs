using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// The constraints a language declares, keyed by concept, in the order they were declared, plus the ones
/// that apply to every node.
/// </summary>
/// <remarks>
/// <para>
/// A concept's constraints are its own plus every super concept's, in declaration order - the same rule
/// notation, triggers and scope providers follow, so a language has one answer to "which declaration
/// applies" rather than one per aspect. A concept that extends another therefore inherits its rules,
/// which is what makes a base concept worth having.
/// </para>
/// <para>
/// A rule about <em>any</em> node is declared with <see cref="Everywhere"/> instead of a concept key, and
/// runs before the concept's own. That is not a convenience: "this reference no longer identifies one
/// element" is a fact about references in general, and stating it once is the difference between a rule and
/// the same rule repeated for every concept that happens to declare a reference slot. Declaring it over a
/// root concept instead would silently skip every concept that does not inherit from that root.
/// </para>
/// <para>
/// The set is immutable and <see cref="Add"/> answers a new one, so a language can be composed without
/// mutating a set another session is using. Several constraints may be declared for one concept; they
/// are all kept, because a rule is a sentence and a concept may need several.
/// </para>
/// </remarks>
public sealed class ConstraintSet
{
    private readonly (string ConceptKey, IConstraint Constraint)[] _declared;
    private readonly IConstraint[] _everywhere;

    private ConstraintSet((string ConceptKey, IConstraint Constraint)[] declared, IConstraint[] everywhere)
    {
        _declared = declared;
        _everywhere = everywhere;
    }

    /// <summary>A set that declares nothing, so nothing is ever reported.</summary>
    public static ConstraintSet Empty { get; } = new([], []);

    /// <summary>How many constraints are declared, the ones for every node included.</summary>
    public int Count => _declared.Length + _everywhere.Length;

    /// <summary>The same set with <paramref name="constraint"/> added for <paramref name="conceptKey"/>.</summary>
    /// <exception cref="ArgumentException">The concept key is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="constraint"/> is <see langword="null"/>.</exception>
    public ConstraintSet Add(string conceptKey, IConstraint constraint)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(constraint);

        return new ConstraintSet([.. _declared, (conceptKey, constraint)], _everywhere);
    }

    /// <summary>The same set with <paramref name="constraint"/> added for every node.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="constraint"/> is <see langword="null"/>.</exception>
    public ConstraintSet Everywhere(IConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        return new ConstraintSet(_declared, [.. _everywhere, constraint]);
    }

    /// <summary>
    /// This set followed by <paramref name="other"/>: the union, in an order a caller can predict, because
    /// findings are reported in the order their rules were declared.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public ConstraintSet With(ConstraintSet other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return other.IsEmpty
            ? this
            : new ConstraintSet([.. _declared, .. other._declared], [.. _everywhere, .. other._everywhere]);
    }

    /// <summary>The rules declared for every node, in declaration order.</summary>
    public IReadOnlyList<IConstraint> EverywhereRules => _everywhere;

    /// <summary>
    /// The constraints that apply to <paramref name="concept"/>: the ones declared for every node, then its
    /// own, then its super concepts', in declaration order.
    /// </summary>
    public IReadOnlyList<IConstraint> For(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (IsEmpty)
        {
            return [];
        }

        List<IConstraint> applicable = [.. _everywhere];

        if (_declared.Length > 0)
        {
            Collect(concept, applicable, []);
        }

        return applicable;
    }

    /// <summary>Whether nothing is declared at all.</summary>
    public bool IsEmpty => _declared.Length == 0 && _everywhere.Length == 0;

    private void Collect(Concept concept, List<IConstraint> applicable, HashSet<string> seen)
    {
        if (!seen.Add(concept.Key))
        {
            return;
        }

        foreach (var (conceptKey, constraint) in _declared)
        {
            if (string.Equals(conceptKey, concept.Key, StringComparison.Ordinal))
            {
                applicable.Add(constraint);
            }
        }

        foreach (var super in concept.SuperConcepts)
        {
            Collect(super, applicable, seen);
        }
    }

    public override string ToString() =>
        _everywhere.Length == 0
            ? $"{_declared.Length} constraint(s)"
            : $"{_declared.Length} constraint(s), {_everywhere.Length} for every node";
}
