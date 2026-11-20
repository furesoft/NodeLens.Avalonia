namespace Projectional.Language;

/// <summary>Both of two types at once: the greatest lower bound of both, as a type a slot may demand.</summary>
/// <remarks>
/// <para>
/// MPS' <c>MeetType</c>, and the other half of the cookbook's pair. As an expectation it reads as an intersection: a
/// value must satisfy both sides, so <see cref="Accepts"/> asks both and takes the answer they agree on.
/// </para>
/// <para>
/// It is the type of "a value that is both", which is rarely a value a language builds - a meet is mostly a
/// statement about the lattice, and <see cref="TypeLattice.Meet"/> is what asks whether it collapses to one type.
/// It is still an <see cref="IType"/> so that a declared rule can name it in the same breath as a join.
/// </para>
/// </remarks>
public sealed class MeetType : IType
{
    /// <summary>Meets <paramref name="left"/> and <paramref name="right"/>.</summary>
    /// <param name="left">One side.</param>
    /// <param name="right">The other side.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public MeetType(IType left, IType right)
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
    public string Name => $"{Left.Name}&{Right.Name}";

    /// <inheritdoc />
    /// <remarks>
    /// Both sides have to accept, because a value of a meet is a value of each. That is why a meet is the stricter
    /// of the two expressions and why it does not inherit the join's leniency: an unknown type fails a meet unless
    /// both sides are lenient about it, which is the honest reading of "it must be both".
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="actual"/> is <see langword="null"/>.</exception>
    public bool Accepts(IType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        return Left.Accepts(actual) && Right.Accepts(actual);
    }

    public override string ToString() => Name;
}
