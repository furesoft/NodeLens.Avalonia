namespace Projectional.Language;

/// <summary>Reduces a <see cref="JoinType"/> or a <see cref="MeetType"/> to one type, when the relation determines one.</summary>
/// <remarks>
/// <para>
/// The cookbook's <c>join(A, B)</c> is not a new kind of type, it is a question about the language's edges: which
/// type is above both, and which is the smallest such. So the answer comes from <see cref="SubtypeRelation"/> and
/// nothing here decides what is below what - a lattice that guessed would be a second type system beside the one a
/// language declared.
/// </para>
/// <para>
/// When the edges do not determine an answer the result is <see cref="UnknownType"/>, which is this framework's way
/// of reporting that a question cannot be answered rather than answering it (and it is distinguishable: a caller
/// that must not proceed on a guess asks <c>type is UnknownType</c>). Two cases lead there, and they are different
/// facts a caller may care about: no type is above both, or several are and none of them is least. Both are
/// reported the same way because the answer a rule needs is "I cannot say", and which of the two it was belongs to
/// a diagnostic about the rule, not to the type it produced.
/// </para>
/// <para>
/// The relation is the concrete <see cref="SubtypeRelation"/> rather than <see cref="ISubtype"/>, because a bound
/// needs the edges and an interface that only answers "is this below that" cannot enumerate them. That is a
/// deliberate limit: a host with its own relation type can still ask the pairwise question, and a lattice over
/// edges it does not publish is not something this framework can compute.
/// </para>
/// </remarks>
public static class TypeLattice
{
    /// <summary>The least type both sides may stand as, or <see cref="UnknownType"/> when there is none.</summary>
    /// <param name="relation">The language's edges.</param>
    /// <param name="left">One type.</param>
    /// <param name="right">The other type.</param>
    /// <remarks>
    /// Writing either side as the other answers that side, which is the common case in a rule: joining a type with
    /// something it already is (or with a subtype of itself) has an answer the relation already holds.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IType Join(SubtypeRelation relation, IType left, IType right)
    {
        ArgumentNullException.ThrowIfNull(relation);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (relation.IsSubtype(right, left))
        {
            return left;
        }

        if (relation.IsSubtype(left, right))
        {
            return right;
        }

        return Least(relation, [.. relation.SupertypesOf(left).Where(candidate => relation.IsSubtype(right, candidate))]);
    }

    /// <summary>The greatest type that may stand as both sides, or <see cref="UnknownType"/> when there is none.</summary>
    /// <param name="relation">The language's edges.</param>
    /// <param name="left">One type.</param>
    /// <param name="right">The other type.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IType Meet(SubtypeRelation relation, IType left, IType right)
    {
        ArgumentNullException.ThrowIfNull(relation);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (relation.IsSubtype(left, right))
        {
            return left;
        }

        if (relation.IsSubtype(right, left))
        {
            return right;
        }

        return Greatest(relation, [.. relation.SubtypesOf(left).Where(candidate => relation.IsSubtype(candidate, right))]);
    }

    /// <summary>The one candidate below every other, or <see cref="UnknownType"/>.</summary>
    /// <remarks>
    /// "Least" is not "first": two incomparable candidates are two answers, and picking one would be the guess this
    /// whole layer refuses. So the answer is unique or it is unknown.
    /// </remarks>
    private static IType Least(SubtypeRelation relation, IReadOnlyList<IType> candidates)
    {
        var minimal = candidates
            .Where(candidate => !candidates.Any(other => IsStrictlyBelow(relation, other, candidate)))
            .ToList();

        return minimal.Count == 1 ? minimal[0] : UnknownType.Instance;
    }

    /// <summary>The one candidate above every other, or <see cref="UnknownType"/>.</summary>
    private static IType Greatest(SubtypeRelation relation, IReadOnlyList<IType> candidates)
    {
        var maximal = candidates
            .Where(candidate => !candidates.Any(other => IsStrictlyBelow(relation, candidate, other)))
            .ToList();

        return maximal.Count == 1 ? maximal[0] : UnknownType.Instance;
    }

    /// <summary>Whether <paramref name="lower"/> is below <paramref name="upper"/> and is not the same type.</summary>
    /// <remarks>
    /// Both directions are asked so that two instances of one primitive - equal by name and not by reference - are
    /// one type rather than two, which is the same comparison <see cref="SubtypeRelation"/> makes internally.
    /// </remarks>
    private static bool IsStrictlyBelow(SubtypeRelation relation, IType lower, IType upper) =>
        relation.IsSubtype(lower, upper) && !relation.IsSubtype(upper, lower);
}
