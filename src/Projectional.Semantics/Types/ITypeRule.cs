namespace Projectional.Semantics;

/// <summary>
/// The aspect that says what a node evaluates to, per concept.
/// </summary>
/// <remarks>
/// <para>
/// It is an aspect rather than a framework rule because a type is a property of the language: an integer
/// literal is an <c>int</c>, a comparison is a <c>bool</c>, and a language that has neither still needs an
/// answer. A concept that declares no rule is <see cref="UnknownType"/>, which is the honest answer -
/// nobody said, so nothing is known.
/// </para>
/// <para>
/// A rule may ask for the type of its own children through <see cref="TypeContext.TypeOf(Node)"/>, which
/// is the whole of "local propagation": a binary expression is as wide as its operands, and nothing here
/// infers further than that.
/// </para>
/// </remarks>
public interface ITypeRule
{
    /// <summary>What <see cref="TypeContext.Node"/> evaluates to.</summary>
    /// <remarks>
    /// Named <c>TypeFor</c> rather than <c>TypeOf</c> because CA1716 forbids a reserved word as an
    /// interface member - the same reason <c>ICheck.Find</c> is not called <c>Of</c>. The class member
    /// <see cref="TypeContext.TypeOf(Node)"/> keeps the natural name, because the rule does not apply to it.
    /// </remarks>
    /// <param name="context">The node and the way to ask about its children.</param>
    IType TypeFor(TypeContext context);
}
