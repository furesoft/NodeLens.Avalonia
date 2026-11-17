using Projectional.Core;
using Projectional.Expressions;
using Projectional.Semantics;

namespace Projectional.Samples.Checks;

/// <summary>
/// The types the expression language's concepts evaluate to.
/// </summary>
/// <remarks>
/// <para>
/// A type rule is behaviour, so it lives beside the language rather than inside it - the same reason
/// intentions and checks do. The language package stays free of the semantics layer, and a host that
/// wants types composes this one.
/// </para>
/// <para>
/// The rules are local propagation and nothing more: a literal is its own type, a parenthesised
/// expression is as wide as what it wraps, a comparison is a <c>bool</c>, and arithmetic is as wide as
/// its left operand. A name is <see cref="UnknownType"/> because what it means depends on a declaration
/// this language does not have - and saying so is better than guessing a type the model never stated.
/// </para>
/// </remarks>
public static class SampleTypes
{
    private static readonly string[] Comparisons = ["==", "!=", "<", ">", "<=", ">=", "&&", "||"];

    /// <summary>Every type rule the sample language declares.</summary>
    public static TypeRegistry All { get; } = TypeRegistry.Empty
        .Add(ExpressionsConceptKeys.IntLiteral, new Literal(PropertyType.Integer))
        .Add(ExpressionsConceptKeys.BoolLiteral, new Literal(PropertyType.Boolean))
        .Add(ExpressionsConceptKeys.StringLiteral, new Literal(PropertyType.String))
        .Add(ExpressionsConceptKeys.Parenthesized, new SameAs("inner"))
        .Add(ExpressionsConceptKeys.Binary, new Binary())
        .Add(ExpressionsConceptKeys.Unary, new Unary())
        .Add(ExpressionsConceptKeys.If, new SameAs("then"));

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
}
