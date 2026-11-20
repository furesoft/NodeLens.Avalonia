namespace Projectional.Language;

/// <summary>The types a language declares interchangeable, beside the edges it declares between them.</summary>
/// <remarks>
/// <para>
/// This is MPS' comparison rule. It exists because "these two may each be used for the other" is a third question,
/// answered neither by conformance nor by subtyping, and because a language may need to say it about two types
/// <em>without</em> claiming either is a subtype of the other. Two declared <see cref="SubtypeRelation"/> edges, one
/// each way, would say the same thing about interchange - and would also put both types in each other's supertype
/// set, so <see cref="TypeLattice"/> would reduce a join of the two to one of them and a walk up from either would
/// cycle back on itself. A comparison states the interchange and leaves the lattice alone.
/// </para>
/// <para>
/// It is symmetric and transitive, which is what makes it the same answer the both-ways subtype reading gives: a
/// declared pair relates its two types in either direction, and a chain of pairs relates its ends. Being identical
/// in meaning to that reading is deliberate - two spellings of one fact must not answer differently - while the
/// lattice stays unaware of the pairs, which is the whole point of having the second spelling.
/// </para>
/// <para>
/// The relation is immutable and <see cref="Add"/> answers a new one, like the edges it sits beside.
/// </para>
/// </remarks>
public sealed class ComparisonRelation
{
    private readonly IReadOnlyList<(IType Left, IType Right)> _pairs;

    private ComparisonRelation(IReadOnlyList<(IType Left, IType Right)> pairs) => _pairs = pairs;

    /// <summary>A relation with no declared pair, so only a type's own sameness relates it to anything.</summary>
    /// <remarks>This is what a language that declares no comparison gets: a type is interchangeable with itself.</remarks>
    public static ComparisonRelation Empty { get; } = new([]);

    /// <summary>How many pairs were declared, which is zero for <see cref="Empty"/>.</summary>
    public int Count => _pairs.Count;

    /// <summary>The same relation with one more pair declared: the two types may each be used for the other.</summary>
    /// <param name="left">One type.</param>
    /// <param name="right">The other.</param>
    /// <remarks>
    /// Declaring a pair the relation already holds answers a relation that holds it once, so a language that states
    /// the same fact in two places does not have to know it did - the same courtesy the edges offer.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Either type is <see cref="UnknownType"/>, which stands in no comparison for the reason it stands in no edge.
    /// </exception>
    public ComparisonRelation Add(IType left, IType right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left is UnknownType || right is UnknownType)
        {
            throw new ArgumentException(
                "'unknown' stands in no comparison: an unknown type is what a rule says it does not know, and letting it be used for a known one is how a guess becomes a proof.",
                nameof(left));
        }

        return IsComparableTo(left, right) ? this : new ComparisonRelation([.. _pairs, (left, right)]);
    }

    /// <summary>Whether each of the two types may stand where the other is expected.</summary>
    /// <param name="left">One type.</param>
    /// <param name="right">The other.</param>
    /// <remarks>
    /// A type is interchangeable with itself, and a declared pair - or a chain of them, read in either direction -
    /// makes two types interchangeable. A pair naming <see cref="UnknownType"/> is refused where it is declared, so
    /// this never has to answer for one.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public bool IsComparableTo(IType left, IType right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return _pairs.Count == 0
            ? SubtypeRelation.Same(left, right)
            : Reaches(left, right, []);
    }

    private bool Reaches(IType from, IType expected, HashSet<IType> seen)
    {
        if (SubtypeRelation.Same(from, expected))
        {
            return true;
        }

        // Reference identity is the visited set, exactly as it is for the edges: a pair graph is a handful of
        // instances, and a cycle it accidentally holds must answer "no" rather than recur forever.
        if (!seen.Add(from))
        {
            return false;
        }

        // Both directions from one pair, which is what "interchangeable" means: a declared pair is one fact, not two.
        return _pairs.Any(pair =>
            (SubtypeRelation.Same(pair.Left, from) && Reaches(pair.Right, expected, seen))
            || (SubtypeRelation.Same(pair.Right, from) && Reaches(pair.Left, expected, seen)));
    }

    public override string ToString() => $"{_pairs.Count} comparable pair(s)";
}
