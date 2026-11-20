namespace Projectional.Language;

/// <summary>
/// What a node evaluates to, as a runtime value.
/// </summary>
/// <remarks>
/// <para>
/// A type is computed, never stored: the model says what a node <em>is</em>, and the type is what that
/// means. Nothing here is persisted, so a type cannot go out of step with the node it describes.
/// </para>
/// <para>
/// The one question a type answers is assignability - "may this stand where that is expected" - and it is
/// asked in that direction on purpose: <c>expected.Accepts(actual)</c> reads the way the rule does, and a
/// caller that got the direction wrong would be comparing two types for equality, which is a different
/// and much weaker question.
/// </para>
/// <para>
/// There is no unification engine and no inference beyond local propagation. A language that needs more
/// says so in its own rules; a framework that guessed would be wrong in ways its author cannot see.
/// </para>
/// </remarks>
public interface IType
{
    /// <summary>How the type is written, which is also what a diagnostic quotes.</summary>
    string Name { get; }

    /// <summary>Whether a value of <paramref name="actual"/> may stand where this type is expected.</summary>
    /// <param name="actual">The type of the value being offered.</param>
    /// <remarks>
    /// <para>
    /// The default answers it with <see cref="SubtypeRelation.Primitives"/>, which is the strict reading: a type
    /// conforms to itself and to what the relation says, and nothing else. A type whose answer is more lenient -
    /// the primitives accept an <see cref="UnknownType"/> so that one mistake in a condition does not become a
    /// finding on every expression that reads it - overrides this and says so where the leniency belongs.
    /// </para>
    /// <para>
    /// The default exists so that a type a language adds needs only its <see cref="Name"/>: conformance is then the
    /// relation's answer rather than a method every implementation has to restate. It deliberately does
    /// <em>not</em> turn <see cref="Accepts"/> into the subtype relation, because the two are different questions
    /// and the difference is where <see cref="UnknownType"/> is kept from answering for a rule that has not been
    /// written.
    /// </para>
    /// <para>
    /// Being a default member, it is reached through <see cref="IType"/> and not through a variable typed as the
    /// implementing class - the runtime's rule for interface defaults, and the reason every consumer in this
    /// framework holds an <see cref="IType"/> rather than a concrete type.
    /// </para>
    /// </remarks>
    bool Accepts(IType actual) => SubtypeRelation.Primitives.IsSubtype(actual, this);
}
