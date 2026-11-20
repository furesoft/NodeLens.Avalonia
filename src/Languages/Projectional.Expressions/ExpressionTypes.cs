using Projectional.Core;
using Projectional.Language;

namespace Projectional.Expressions;

/// <summary>
/// What the expression language's concepts evaluate to.
/// </summary>
/// <remarks>
/// <para>
/// A type is a statement about what a language means, so it is declared here rather than in the layer
/// that consumes it - the same reason the notation and the typing rules live in the language package. A
/// host gets <see cref="ExpressionsLanguage.Types"/> by referencing <c>Projectional.Expressions</c>, with
/// nothing else to compose.
/// </para>
/// <para>
/// The rules are local propagation and nothing more: a literal is its own type, a parenthesised
/// expression is as wide as what it wraps, a comparison is a <c>bool</c>, and arithmetic is as wide as
/// its left operand. An <c>if</c> is as wide as its two branches, which is a question about the relation
/// rather than about one child - see <see cref="Branches"/>. A name is <see cref="UnknownType"/> because
/// what it means depends on a declaration this language does not have - and saying so is better than
/// guessing a type the model never stated.
/// </para>
/// </remarks>
internal static class ExpressionTypes
{
    private static readonly string[] Comparisons = ["==", "!=", "<", ">", "<=", ">=", "&&", "||"];

    /// <summary>The edges this language declares, which is none.</summary>
    /// <remarks>
    /// Named rather than inlined so that the join below and the registry carry the same relation: a language that
    /// grows an edge adds it here and both the solver and the <c>if</c> see it.
    /// </remarks>
    internal static SubtypeRelation Subtypes { get; } = SubtypeRelation.Primitives;

    /// <summary>The registry <see cref="ExpressionsLanguage.Types"/> answers with.</summary>
    internal static TypeRegistry Registry { get; } = TypeRegistry.Empty
        .Add(ExpressionsConceptKeys.IntLiteral, new Literal(PropertyType.Integer))
        .Add(ExpressionsConceptKeys.BoolLiteral, new Literal(PropertyType.Boolean))
        .Add(ExpressionsConceptKeys.StringLiteral, new Literal(PropertyType.String))
        .Add(ExpressionsConceptKeys.Parenthesized, new SameAs("inner"))
        .Add(ExpressionsConceptKeys.Binary, new Binary())
        .Add(ExpressionsConceptKeys.Unary, new Unary())
        .Add(ExpressionsConceptKeys.If, new Branches(Subtypes))
        .WithSubtypes(Subtypes);

    private sealed class Literal(PropertyType type) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => PrimitiveType.Of(type);
    }

    /// <summary>An expression that is as wide as the child it wraps.</summary>
    private sealed class SameAs(string slot) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => context.TypeOfChild(slot);
    }

    /// <summary>
    /// A comparison is a <c>bool</c>; arithmetic is as wide as its left operand.
    /// </summary>
    /// <remarks>
    /// The operator decides, not the operands, because that is what the language means: <c>a &lt; b</c> is
    /// a truth value whatever <c>a</c> and <c>b</c> are. An operator the language does not know is
    /// <see cref="UnknownType"/> rather than a guess.
    /// </remarks>
    private sealed class Binary : ITypeRule
    {
        public IType TypeFor(TypeContext context)
        {
            var op = context.Node.GetOrNull("operator");

            if (op is null)
            {
                return UnknownType.Instance;
            }

            return Comparisons.Contains(op, StringComparer.Ordinal)
                ? PrimitiveType.Of(PropertyType.Boolean)
                : context.TypeOfChild("left");
        }
    }

    /// <summary>Negation is a <c>bool</c>; a minus sign is as wide as its operand.</summary>
    private sealed class Unary : ITypeRule
    {
        public IType TypeFor(TypeContext context) =>
            context.Node.GetOrNull("operator") == "!"
                ? PrimitiveType.Of(PropertyType.Boolean)
                : context.TypeOfChild("operand");
    }

    /// <summary>
    /// A conditional is as wide as its two branches together: the type a value of either branch may be read as.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one rule here that is not local propagation, and it is why the type layer has a lattice: "as wide
    /// as its then-branch" answers <c>int</c> for <c>if c then 1 else "x"</c>, which is a type the node does not
    /// have. The join answers the common supertype, and when there is none it answers <see cref="UnknownType"/> -
    /// "I cannot say" rather than a claim the author never made.
    /// </para>
    /// <para>
    /// With the <c>else</c> unwritten the then-branch is the whole answer, because that is the ordinary state of a
    /// document being edited and a rule that made it unknown would make typing an <c>if</c> lose the type it just
    /// earned. The two-branch question is asked only when there are two branches.
    /// </para>
    /// </remarks>
    private sealed class Branches(SubtypeRelation subtypes) : ITypeRule
    {
        public IType TypeFor(TypeContext context) =>
            context.Node.GetChild("else") is null
                ? context.TypeOfChild("then")
                : TypeLattice.Join(subtypes, context.TypeOfChild("then"), context.TypeOfChild("else"));
    }
}
