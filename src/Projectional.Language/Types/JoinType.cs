namespace Projectional.Language;

/// <summary>Either of two types: the least upper bound of both, as a type a slot may demand.</summary>
/// <remarks>
/// <para>
/// It is what a language writes where a slot accepts one type or another - MPS' <c>JoinType</c>, and the reason the
/// cookbook's <c>join(a, b)</c> has a spelling at all. As an expectation it reads as a union: a value of either side
/// may stand here, so <see cref="Accepts"/> asks the two sides and takes either answer.
/// </para>
/// <para>
/// It stays a type expression rather than being reduced to one type, because the reduction is a question about the
/// language's edges and not about the join: <see cref="TypeLattice.Join"/> answers it, and answers
/// <see cref="UnknownType"/> when the relation does not determine one. Keeping the expression means a document can
/// say "either of these" before any rule knows which.
/// </para>
/// <para>
/// The sides are kept in the order they were written and not de-duplicated: a join is also a spelling, and the
/// name it draws is the one the author sees.
/// </para>
/// </remarks>
public sealed class JoinType : IType
{
    /// <summary>Joins <paramref name="left"/> and <paramref name="right"/>.</summary>
    /// <param name="left">One side.</param>
    /// <param name="right">The other side.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public JoinType(IType left, IType right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        Left = left;
        Right = right;
    }

    /// <summary>The first side, as it was written.</summary>
    public IType Left { get; }

    /// <summary>The second side, as it was written.</summary>
    public IType Right { get; }

    /// <inheritdoc />
    public string Name => $"{Left.Name}|{Right.Name}";

    /// <inheritdoc />
    /// <remarks>
    /// Either side accepting is enough, which is what "may be one or the other" means. The sides decide, so a join
    /// of a primitive and an alias behaves the way those two do - including accepting an unknown type, because a
    /// union must not turn one mistake into a finding.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="actual"/> is <see langword="null"/>.</exception>
    public bool Accepts(IType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        return Left.Accepts(actual) || Right.Accepts(actual);
    }

    public override string ToString() => Name;
}
