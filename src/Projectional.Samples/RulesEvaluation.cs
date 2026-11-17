using Projectional.Expressions;
using Projectional.Language;

namespace Projectional.Samples;

/// <summary>What a rule and a ruleset evaluate to: a decision list.</summary>
/// <remarks>
/// <para>
/// A rule is a <b>predicate</b>: its value is the truth of its condition, so a rule drawn on screen can be read
/// as "does this one apply". A ruleset is the <b>decision</b>: the first rule whose condition holds decides the
/// outcome, which is what makes the document a rule table rather than a list of independent tests. Both are in
/// the trace, so "why did we accept this" is answered by the lines under the ruleset.
/// </para>
/// <para>
/// A ruleset with no matching rule is a failure, not a default: <see cref="RuleOutcomes.Review"/> is a decision a
/// reviewer makes, and inventing it here would hide a table whose conditions leave a gap. The message says so,
/// and the host decides what to show.
/// </para>
/// </remarks>
public static class RulesEvaluation
{
    /// <summary>The rules of <see cref="RulesLanguage"/>, composed on top of the expression rules.</summary>
    /// <remarks>
    /// A ruleset is only runnable because a condition is an expression, so this registry <em>contains</em> the
    /// expression rules rather than expecting a host to bolt them on: one registry per document, the same way
    /// the concept registry is one composition.
    /// </remarks>
    public static EvaluationRegistry Registry { get; } = ExpressionsEvaluation.Registry
        .Add(RulesConceptKeys.Rule, new DelegateEvaluationRule(RuleApplies))
        .Add(RulesConceptKeys.Ruleset, new DelegateEvaluationRule(FirstMatch));

    private static BoolValue RuleApplies(EvaluationContext context) =>
        context.EvalChild("condition") is BoolValue condition
            ? condition
            : throw context.Error("A rule's condition must be a truth value.");

    private static StringValue FirstMatch(EvaluationContext context)
    {
        foreach (var rule in context.Node.GetChildren(RulesLanguage.RulesSlot))
        {
            if (context.Eval(rule) is BoolValue { Content: true })
            {
                return new StringValue(rule.GetOrNull("outcome") ?? RuleOutcomes.Review);
            }
        }

        throw context.Error("No rule matched, so the ruleset has no outcome.");
    }
}
