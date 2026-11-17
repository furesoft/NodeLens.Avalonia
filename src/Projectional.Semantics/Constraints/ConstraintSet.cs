using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// The constraints a language declares, keyed by concept, in the order they were declared.
/// </summary>
/// <remarks>
/// <para>
/// A concept's constraints are its own plus every super concept's, in declaration order - the same rule
/// notation, triggers and scope providers follow, so a language has one answer to "which declaration
/// applies" rather than one per aspect. A concept that extends another therefore inherits its rules,
/// which is what makes a base concept worth having.
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

    private ConstraintSet((string ConceptKey, IConstraint Constraint)[] declared) => _declared = declared;

    /// <summary>A set that declares nothing, so nothing is ever reported.</summary>
    public static ConstraintSet Empty { get; } = new([]);

    /// <summary>How many constraints are declared.</summary>
    public int Count => _declared.Length;

    /// <summary>The same set with <paramref name="constraint"/> added for <paramref name="conceptKey"/>.</summary>
    /// <exception cref="ArgumentException">The concept key is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="constraint"/> is <see langword="null"/>.</exception>
    public ConstraintSet Add(string conceptKey, IConstraint constraint)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(constraint);

        return new ConstraintSet([.. _declared, (conceptKey, constraint)]);
    }

    /// <summary>
    /// The constraints that apply to <paramref name="concept"/>: its own, then its super concepts', in
    /// declaration order.
    /// </summary>
    public IReadOnlyList<IConstraint> For(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (_declared.Length == 0)
        {
            return [];
        }

        List<IConstraint> applicable = [];
        HashSet<string> seen = [];

        Collect(concept, applicable, seen);

        return applicable;
    }

    /// <summary>Whether any concept declares a constraint at all.</summary>
    public bool IsEmpty => _declared.Length == 0;

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

    public override string ToString() => $"{_declared.Length} constraint(s)";
}
