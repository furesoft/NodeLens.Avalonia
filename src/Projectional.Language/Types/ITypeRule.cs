namespace Projectional.Language;

/// <summary>
/// The aspect that says what a node evaluates to, per concept.
/// </summary>
/// <remarks>
/// <para>
/// It lives in <c>Projectional.Language</c>, beside the notation and the typing rules, because a type is a
/// statement about what a language means - so the language package that declares concepts must be able to
/// declare their types without referencing the semantics layer that consumes them. The consumers (the
/// constraint engine, the declared type-rule language, a host) sit above and read the registry this
/// assembly hands them.
/// </para>
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

    /// <summary>The relations this rule states about its node, or nothing when it simply answers a type.</summary>
    /// <param name="context">The node and the way to ask about its children.</param>
    /// <remarks>
    /// <para>
    /// A rule whose answer depends on another node's type cannot be written as <see cref="TypeFor"/>: it does not
    /// have that answer yet, and returning a <see cref="TypeVariable"/> would hand a caller an unresolved name
    /// with nothing to resolve it. So a rule may instead <em>state</em> relations, which
    /// <see cref="TypeSolver"/> solves together with every other rule's.
    /// </para>
    /// <para>
    /// The default states nothing, which is what makes this additive: every rule that already answers a type keeps
    /// doing so, and a consumer asks for equations first and falls back to <see cref="TypeFor"/> when a rule has
    /// none. A rule that states equations may leave <see cref="TypeFor"/> answering a variable - the solver is
    /// what gives it a value - and that is the only case where a caller sees a <see cref="TypeVariable"/> at all.
    /// </para>
    /// </remarks>
    IEnumerable<TypeEquation> EquationsFor(TypeContext context) => [];
}
