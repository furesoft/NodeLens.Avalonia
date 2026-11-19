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
/// its left operand. A name is <see cref="UnknownType"/> because what it means depends on a declaration
/// this language does not have - and saying so is better than guessing a type the model never stated.
/// </para>
/// </remarks>
internal static class ExpressionTypes
{
    private static readonly string[] Comparisons = ["==", "!=", "<", ">", "<=", ">=", "&&", "||"];

    /// <summary>The registry <see cref="ExpressionsLanguage.Types"/> answers with.</summary>
    internal static TypeRegistry Registry { get; } = TypeRegistry.Empty
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
