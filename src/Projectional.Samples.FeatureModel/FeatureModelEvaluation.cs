using Projectional.Core;
using Projectional.Language;

namespace Projectional.Samples.FeatureModel;

/// <summary>What a feature model means: whether its constraints hold, and which features that leaves selected.</summary>
/// <remarks>
/// <para>
/// Each constraint is a predicate on the selection - "does this rule hold" - and the model is the walk that runs
/// all of them and then answers the configuration. That is the same split the state machine makes between an
/// arrow and the walk, and it is what the trace is for: one line per constraint checked, so "which rule
/// rejected my configuration" is answered by reading them.
/// </para>
/// <para>
/// The violating constraint is the one that raises the failure, not the model, so the interpreter records the
/// <em>inner</em> node: a user jumps to the rule that broke rather than to the document.
/// </para>
/// <para>
/// A feature has no rule of its own. Asking what one feature is would have to invent an answer nobody asked for,
/// and the interpreter reports a missing rule rather than guessing - the rule the whole aspect follows.
/// </para>
/// </remarks>
public static class FeatureModelEvaluation
{
    /// <summary>The rules of <see cref="FeatureModelLanguage"/>, in one registry.</summary>
    public static EvaluationRegistry Registry { get; } = EvaluationRegistry.Empty
        .Add(FmConceptKeys.Requires, new DelegateEvaluationRule(RequiresHolds))
        .Add(FmConceptKeys.Excludes, new DelegateEvaluationRule(ExcludesHolds))
        .Add(FmConceptKeys.Model, new DelegateEvaluationRule(Selection));

    /// <summary>A feature that is selected must have its required feature selected too.</summary>
    private static BoolValue RequiresHolds(EvaluationContext context)
    {
        var constraint = context.Node;
        var subject = FeatureNameOf(constraint, FeatureModelLanguage.SubjectSlot);
        var other = FeatureNameOf(constraint, FeatureModelLanguage.OtherSlot);

        if (IsSelected(constraint, FeatureModelLanguage.SubjectSlot) && !IsSelected(constraint, FeatureModelLanguage.OtherSlot))
        {
            throw context.Error($"\"{subject}\" is selected, so \"{other}\" must be selected too.");
        }

        return new BoolValue(true);
    }

    /// <summary>A feature that is selected must have its excluded feature left out.</summary>
    private static BoolValue ExcludesHolds(EvaluationContext context)
    {
        var constraint = context.Node;
        var subject = FeatureNameOf(constraint, FeatureModelLanguage.SubjectSlot);
        var other = FeatureNameOf(constraint, FeatureModelLanguage.OtherSlot);

        if (IsSelected(constraint, FeatureModelLanguage.SubjectSlot) && IsSelected(constraint, FeatureModelLanguage.OtherSlot))
        {
            throw context.Error($"\"{subject}\" and \"{other}\" cannot both be selected.");
        }

        return new BoolValue(true);
    }

    /// <summary>Every constraint holds, so the answer is the configuration it leaves.</summary>
    private static StringValue Selection(EvaluationContext context)
    {
        var model = context.Node;

        foreach (var constraint in model.GetChildren(FeatureModelLanguage.ConstraintsSlot))
        {
            // Evaluated for its answer and for its refusal: a rule that holds answers true, and the one that
            // does not has already raised the failure that names it.
            _ = context.Eval(constraint);
        }

        List<string> selected = [];

        foreach (var feature in model.GetChildren(FeatureModelLanguage.FeaturesSlot))
        {
            if (feature.GetOrNull(FeatureModelLanguage.SelectedSlot) == "true")
            {
                selected.Add(NameOf(feature));
            }
        }

        return new StringValue(selected.Count == 0 ? "<nothing selected>" : string.Join(", ", selected));
    }

    private static bool IsSelected(Node constraint, string slot) =>
        constraint.GetReferenceTarget(slot)?.GetOrNull(FeatureModelLanguage.SelectedSlot) == "true";

    private static string FeatureNameOf(Node constraint, string slot) =>
        constraint.GetReferenceTarget(slot) is { } feature ? NameOf(feature) : "<unlinked>";

    private static string NameOf(Node feature) =>
        feature.GetOrNull("name") is { Length: > 0 } name ? name : "<unnamed>";
}
