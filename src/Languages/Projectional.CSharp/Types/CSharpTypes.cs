using Projectional.Core;
using Projectional.Language;

namespace Projectional.CSharp.Types;

/// <summary>What a C# node evaluates to, which is what turns the rules into findings.</summary>
/// <remarks>
/// <para>
/// The subset is what a projection can know without a compiler: a literal's type, the type a name borrows from the
/// declaration it resolves to, and the type an operator produces from its operands. Nothing here infers further than
/// local propagation - a call's return type and a member's type need a declaration to read, and C# looks those up in
/// assemblies this language does not model.
/// </para>
/// <para>
/// The honest answer where nothing is known is <see cref="UnknownType"/> rather than a guess, and the rules that
/// report findings have to treat it as "no news": a member access whose type is unknown must not become a finding on
/// every expression that reads it, which is the mistake that makes a checker unusable.
/// </para>
/// <para>
/// A collection expression is deliberately unknown as well. C# 12's <c>[1, 2, 3]</c> is target-typed - the variable it
/// is assigned to decides what it becomes - so a rule that answered <c>int[]</c> would be right only for the
/// assignment it happened to see.
/// </para>
/// <para>
/// The three expressions whose subject is a type split the same way. A cast is the type it names, because saying what
/// the value is read as is the whole of what a cast does; an <c>is</c> is a truth value whatever its operands are; and
/// an index is unknown, because what <c>target[index]</c> returns is a fact about the target's type - an array, a
/// string, a list - and this language models none of them. Reading a type from the target would be a guess that a
/// string's index is a <c>char</c> and an array's is its element type, and a wrong guess is worse than "no news".
/// </para>
/// </remarks>
public static class CSharpTypes
{
    private static readonly PrimitiveType Whole = PrimitiveType.Of(PropertyType.Integer);

    private static readonly PrimitiveType Fractional = PrimitiveType.Of(PropertyType.Float);

    private static readonly PrimitiveType Bool = PrimitiveType.Of(PropertyType.Boolean);

    private static readonly PrimitiveType Text = PrimitiveType.Of(PropertyType.String);

    /// <summary>The type rules the base language states.</summary>
    public static TypeRegistry Registry { get; } = TypeRegistry.Empty
        .Add(CSharpConceptKeys.IntLiteral, new Constant(Whole))
        .Add(CSharpConceptKeys.LongLiteral, new Constant(Whole))
        .Add(CSharpConceptKeys.DoubleLiteral, new Constant(Fractional))
        .Add(CSharpConceptKeys.StringLiteral, new Constant(Text))
        .Add(CSharpConceptKeys.BoolLiteral, new Constant(Bool))
        .Add(CSharpConceptKeys.TypeReference, new TypeReferenceType())
        .Add(CSharpConceptKeys.Field, new SameAs("type"))
        .Add(CSharpConceptKeys.Parameter, new SameAs("type"))
        .Add(CSharpConceptKeys.LocalDeclaration, new LocalType())
        .Add(CSharpConceptKeys.ForInitializer, new LocalType())
        .Add(CSharpConceptKeys.Property, new SameAs("type"))
        .Add(CSharpConceptKeys.Name, new NameType())
        .Add(CSharpConceptKeys.Parenthesized, new SameAs("inner"))
        .Add(CSharpConceptKeys.Binary, new BinaryType())
        .Add(CSharpConceptKeys.Unary, new UnaryType())
        .Add(CSharpConceptKeys.Conditional, new SameAs(CSharpLanguage.WhenTrueSlot))
        .Add(CSharpConceptKeys.Cast, new SameAs("type"))
        .Add(CSharpConceptKeys.Is, new Constant(Bool))
        .Add(CSharpConceptKeys.As, new SameAs("type"));

    /// <summary>
    /// A written type, mapped to the primitive it names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `int`, `long`, `double`, `string` and `bool` are the five the language's literals produce, so mapping them keeps
    /// a declaration and a literal in one vocabulary: `int count = 0` is then an <c>int</c> on both sides, which is
    /// what lets a comparison of the two be checked at all. `long` shares the whole-number primitive with `int`,
    /// because the framework's closed property-type set has one integer - the two are told apart by the literal's
    /// suffix, not by a type the model can hold.
    /// </para>
    /// <para>
    /// Anything else is <see cref="UnknownType"/> on purpose. `Greeter` or `System.Date` names a type this language
    /// cannot describe - it has no declaration to read - and inventing one would make every rule that reads it wrong
    /// in the same direction. The name is still drawn and still generates; only the type is not claimed.
    /// </para>
    /// </remarks>
    private sealed class TypeReferenceType : ITypeRule
    {
        public IType TypeFor(TypeContext context) => context.Node.GetOrNull(CSharpLanguage.NameSlot) switch
        {
            "int" or "long" => Whole,
            "double" => Fractional,
            "string" => Text,
            "bool" => Bool,
            _ => UnknownType.Instance,
        };
    }
    /// <summary>A literal has the type its property type names.</summary>
    private sealed class Constant(PrimitiveType type) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => type;
    }

    /// <summary>An expression that is as wide as the child it wraps.</summary>
    private sealed class SameAs(string slot) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => context.TypeOfChild(slot);
    }

    /// <summary>
    /// A local declaration - and the counter a <c>for</c> declares - is as wide as the type it writes, or as its
    /// initializer when it says <c>var</c>.
    /// </summary>
    /// <remarks>
    /// <c>var</c> is not a type, it is the decision not to write one: the compiler takes the initializer's. Reading the
    /// type slot there would answer a type the author deliberately left out - and the slot is empty in that case, so
    /// the honest answer has to come from the slot that does hold something. A <c>var</c> with no initializer is
    /// therefore <see cref="UnknownType"/>, which is also the truthful answer: there is nothing to take a type from.
    /// </remarks>
    private sealed class LocalType : ITypeRule
    {
        public IType TypeFor(TypeContext context) =>
            context.Node.GetBool(CSharpLanguage.IsVarSlot)
                ? context.TypeOfChild(CSharpConceptKeys.ValueSlot)
                : context.TypeOfChild("type");
    }

    /// <summary>
    /// A name has the type of the declaration it refers to, and an unresolved one has none.
    /// </summary>
    /// <remarks>
    /// This is why <see cref="CSharpConceptKeys.Name"/> is a reference and not a property: the type of a name is not a
    /// fact about the name, it is a fact about the declaration, and a reference is what gets there. A name that points
    /// at nothing is <see cref="UnknownType"/> rather than an error - the missing target is what is wrong with it, and
    /// reporting a type as well would be a second finding for one mistake.
    /// </remarks>
    private sealed class NameType : ITypeRule
    {
        public IType TypeFor(TypeContext context) =>
            context.Node.GetReferenceTarget(CSharpConceptKeys.TargetSlot) is { } declaration
                ? context.TypeOf(declaration)
                : UnknownType.Instance;
    }

    /// <summary>
    /// A negation is a truth value, and every other unary operator is as wide as the operand it applies to.
    /// </summary>
    /// <remarks>
    /// The same division the binary operators make, one operand shorter: `!ready` is a `bool` whatever `ready` is,
    /// while `-count` has the type `count` has. A ternary takes the type of the branch it would choose, which is the
    /// honest answer while the language has no join of two branches - `true ? 1 : "x"` is a mistake this cannot see.
    /// </remarks>
    private sealed class UnaryType : ITypeRule
    {
        public IType TypeFor(TypeContext context) =>
            context.Node.GetOrNull(CSharpLanguage.UnaryOperatorSlot) == CSharpUnaryOperator.Not
                ? Bool
                : context.TypeOfChild(CSharpConceptKeys.OperandSlot);
    }
    /// <summary>
    /// The operator decides: a comparison or a logical operator is a truth value whatever its operands are, and
    /// arithmetic is as wide as its left operand.
    /// </summary>
    /// <remarks>
    /// An operator the language does not know answers <see cref="UnknownType"/> instead of guessing, and so does an
    /// operand this language cannot type - a rule that invented an <c>int</c> for an unknown operand would then
    /// report the comparison as fine.
    /// </remarks>
    private sealed class BinaryType : ITypeRule
    {
        public IType TypeFor(TypeContext context) => context.Node.GetOrNull(CSharpLanguage.OperatorSlot) switch
        {
            CSharpBinaryOperator.Equal or CSharpBinaryOperator.NotEqual
                or CSharpBinaryOperator.LessThan or CSharpBinaryOperator.GreaterThan
                or CSharpBinaryOperator.And or CSharpBinaryOperator.Or => Bool,
            CSharpBinaryOperator.Add or CSharpBinaryOperator.Subtract
                or CSharpBinaryOperator.Multiply or CSharpBinaryOperator.Divide => context.TypeOfChild("left"),
            _ => UnknownType.Instance,
        };
    }
}
