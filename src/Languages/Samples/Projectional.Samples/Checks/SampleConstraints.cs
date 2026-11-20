using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Semantics;

namespace Projectional.Samples.Checks;

/// <summary>
/// The rules the built-in sample languages state about themselves, as constraints rather than as checks.
/// </summary>
/// <remarks>
/// <para>
/// A constraint is the language's own sentence about a node; a check is a package that also knows how to
/// repair what it finds. The sample has both, and the split is the point: "a rule needs a condition" is
/// something the language says, while "this name is still the default" is something a catalogue says
/// about any language.
/// </para>
/// <para>
/// A rule about a node of another language lives here too when that language cannot state it itself: the
/// expression language's operand rule is about an <c>Expressions.Binary</c>, but the package that declares
/// that concept deliberately does not reference the semantics layer, and this one does. Keying a constraint
/// by a concept is what makes the two languages' rules live side by side in one set - a document is checked
/// with every rule whose concept it contains, and a document that contains none of them is unaffected.
/// </para>
/// <para>
/// A constraint reports and never repairs, so these findings reach the problems panel and the squiggles
/// but not the quick-fix list. That is deliberate for the first one: there is no single condition to
/// invent, and a repair that guessed would be worse than a sentence that says what is missing.
/// </para>
/// </remarks>
public static class SampleConstraints
{
    /// <summary>The identifier the missing-condition rule reports under.</summary>
    public const string MissingConditionCode = "rules.condition.required";

    /// <summary>The identifier the non-boolean-condition rule reports under.</summary>
    public const string ConditionNotBooleanCode = "rules.condition.not-boolean";

    /// <summary>The identifier the operand rule reports under.</summary>
    public const string OperandsNotComparableCode = "expressions.operands.not-comparable";

    /// <summary>Every constraint the built-in sample documents are checked with.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty
        .Add(RulesConceptKeys.Rule, new MissingCondition())
        .Add(RulesConceptKeys.Rule, new ConditionMustBeBoolean())
        .Add(ExpressionsConceptKeys.Binary, new OperandsNotComparable());

    /// <summary>A rule without a condition, which is the design's own example of a constraint.</summary>
    private sealed class MissingCondition : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            if (context.Node.GetChildren("condition").Count == 0)
            {
                context.Error(
                    "a rule needs a condition.",
                    role: "condition",
                    code: MissingConditionCode);
            }
        }
    }

    /// <summary>
    /// A condition that is not a truth value, which is the first constraint that needs a type.
    /// </summary>
    /// <remarks>
    /// An <see cref="UnknownType"/> is accepted: a condition whose type could not be computed is a
    /// different finding (or none), and reporting it here would turn one mistake into two. That is the
    /// whole reason <c>Unknown</c> is a type rather than a null.
    /// </remarks>
    private sealed class ConditionMustBeBoolean : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            var conditions = context.Node.GetChildren("condition");

            if (conditions.Count == 0)
            {
                return;
            }

            var type = context.TypeOf(conditions[0]);

            if (!PrimitiveType.Of(PropertyType.Boolean).Accepts(type))
            {
                context.Error(
                    $"a condition has to be true or false, but this one is {type.Name}.",
                    role: "condition",
                    code: ConditionNotBooleanCode);
            }
        }
    }

    /// <summary>
    /// An operation whose two operands cannot be used together: <c>1 + "x"</c>, <c>1 &lt; "x"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the "operation is not applicable to these operands" rule, and it is why the expression
    /// language needs a type at all: without it <c>1 + "x"</c> is typed as an <c>int</c> and nothing is
    /// reported, because the operator decides the type and never looks at what it was given.
    /// </para>
    /// <para>
    /// Comparability is the question, not sameness: two operands of one operator have to be usable one for
    /// the other, and asking whether they are the same type would refuse <c>1 + 2</c> the moment a language
    /// declared two numeric types interchangeable. It is asked of the type first and then of the registry's
    /// relations, which is the same order the declared <c>TypeRule.NotComparable</c> condition asks it in -
    /// two authoring paths, one answer.
    /// </para>
    /// <para>
    /// An <see cref="UnknownType"/> operand is not reported: it is what a rule says it does not know, so a
    /// name this language cannot type would otherwise turn one missing answer into a finding on every
    /// expression that uses it. An operator or a side that is not filled in is not reported either - the
    /// node is merely incomplete, which is not the author's mistake to be told about.
    /// </para>
    /// </remarks>
    private sealed class OperandsNotComparable : IConstraint
    {
        public void Check(ConstraintContext context)
        {
            var op = context.Node.GetOrNull("operator");

            if (op is null
                || context.Node.GetChild("left") is not { } left
                || context.Node.GetChild("right") is not { } right)
            {
                return;
            }

            var leftType = context.TypeOf(left);
            var rightType = context.TypeOf(right);

            if (leftType.IsComparableTo(rightType)
                || context.Types.Subtypes.IsComparableTo(leftType, rightType)
                || context.Types.Comparisons.IsComparableTo(leftType, rightType))
            {
                return;
            }

            context.Error(
                $"the operands of '{op}' are not comparable: {leftType.Name} and {rightType.Name}.",
                role: "operator",
                code: OperandsNotComparableCode);
        }
    }
}
