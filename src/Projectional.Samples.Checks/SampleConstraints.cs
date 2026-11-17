using Projectional.Core;
using Projectional.Samples;
using Projectional.Semantics;

namespace Projectional.Samples.Checks;

/// <summary>
/// The rules the sample language states about itself, as constraints rather than as checks.
/// </summary>
/// <remarks>
/// <para>
/// A constraint is the language's own sentence about a node; a check is a package that also knows how to
/// repair what it finds. The sample has both, and the split is the point: "a rule needs a condition" is
/// something the language says, while "this name is still the default" is something a catalogue says
/// about any language.
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

    /// <summary>Every constraint the sample language declares.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty
        .Add(RulesConceptKeys.Rule, new MissingCondition())
        .Add(RulesConceptKeys.Rule, new ConditionMustBeBoolean());

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
}
