namespace Projectional.Semantics;

/// <summary>
/// The rules the framework states about any scope, for a host to compose with its language's own.
/// </summary>
/// <remarks>
/// <para>
/// These are not a language's sentences about its concepts - they are what the scope model itself promises,
/// so every host that has references wants them and none of them should have to be written again. A host
/// composes them beside its language's constraints:
/// <c>SampleConstraints.All.With(ScopeConstraints.All)</c>.
/// </para>
/// <para>
/// They are constraints rather than checks because none of them can be repaired: what should happen to a
/// reference whose name became ambiguous is the user's decision, and a check is a rule that always carries
/// the repair which removes it.
/// </para>
/// </remarks>
public static class ScopeConstraints
{
    /// <summary>Every scope rule the framework ships.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty.Everywhere(new AmbiguousReferenceConstraint());
}
