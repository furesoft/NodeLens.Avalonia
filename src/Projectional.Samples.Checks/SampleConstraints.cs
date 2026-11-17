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

    /// <summary>Every constraint the sample language declares.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty
        .Add(RulesConceptKeys.Rule, new MissingCondition());

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
}
