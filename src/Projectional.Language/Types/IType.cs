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
    bool Accepts(IType actual);
}
