namespace Projectional.Language;

/// <summary>The one question a subtype relation answers: may a value of one type stand where another is expected?</summary>
/// <remarks>
/// <para>
/// It is asked in the same direction as <see cref="IType.Accepts"/> - <c>expected</c> is the type the language
/// requires, <c>actual</c> the type the node produces - so a caller that has one does not have to flip its
/// arguments for the other. Asking it the other way round is what makes a rule read as a comparison of two types
/// for equality, which is a different and much weaker question.
/// </para>
/// <para>
/// It is separate from <see cref="IType.Accepts"/> on purpose, and that separation is the point of this
/// interface. <em>Conformance</em> is "may this value stand in this slot", and an unknown type answers it
/// leniently so that one mistake does not produce fifty findings. <em>Subtyping</em> is what the language's
/// type rules say, and it is strict - an unknown type is not a subtype of <c>int</c>, because a rule that asked
/// would otherwise be told a guess is a proof. Collapsing the two is how <see cref="UnknownType"/> leaks out of
/// the one place it is meant to live.
/// </para>
/// </remarks>
public interface ISubtype
{
    /// <summary>Whether a value of type <paramref name="actual"/> may stand where <paramref name="expected"/> is required.</summary>
    /// <param name="actual">The type the node produces.</param>
    /// <param name="expected">The type the language requires.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    bool IsSubtype(IType actual, IType expected);
}
