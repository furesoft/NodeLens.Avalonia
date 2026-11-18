using Projectional.Core;
using Projectional.Language;
using Projectional.Samples.FeatureModel;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a feature model means when it is run: every constraint is a predicate on the selection, and the model
/// answers the configuration those rules leave.
/// </summary>
/// <remarks>
/// The model under test is `Core -requires-&gt; Safe`, `Ui -requires-&gt; Core` and
/// `Safe -excludes-&gt; Experimental`, so the interesting runs are the configuration that holds and the two ways a
/// selection can break a rule.
/// </remarks>
public sealed class FeatureModelEvaluationTests
{
    [Fact]
    public void AConfigurationWhereEveryRuleHoldsAnswersTheSelection()
    {
        var (model, root) = Model();
        var core = Feature(model, root, "Core", selected: true);
        var safe = Feature(model, root, "Safe", selected: true);
        Feature(model, root, "Experimental");

        Constraint(model, root, FmConceptKeys.Requires, core, safe);
        Constraint(model, root, FmConceptKeys.Excludes, safe, Feature(model, root, "Unused"));

        var result = Run(root);

        Assert.True(result.Succeeded);
        Assert.Equal(new StringValue("Core, Safe"), result.Value);
    }

    [Fact]
    public void ARequiredFeatureThatIsNotSelectedIsAFailureAboutItsRule()
    {
        var (model, root) = Model();
        var core = Feature(model, root, "Core", selected: true);
        var safe = Feature(model, root, "Safe");
        var rule = Constraint(model, root, FmConceptKeys.Requires, core, safe);

        var result = Run(root);

        Assert.False(result.Succeeded);
        Assert.Equal(rule.Id, result.Failure!.Node!.Value);
        Assert.Contains("\"Core\"", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("\"Safe\"", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExcludedFeatureThatIsSelectedIsAFailureAboutItsRule()
    {
        var (model, root) = Model();
        var safe = Feature(model, root, "Safe", selected: true);
        var experimental = Feature(model, root, "Experimental", selected: true);
        var rule = Constraint(model, root, FmConceptKeys.Excludes, safe, experimental);

        var result = Run(root);

        Assert.False(result.Succeeded);
        Assert.Equal(rule.Id, result.Failure!.Node!.Value);
        Assert.Contains("cannot both be selected", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleAboutAFeatureThatIsNotSelectedSaysNothing()
    {
        // The rule is a conditional, not a prohibition: `Safe excludes Experimental` does not forbid selecting
        // Experimental, it forbids doing so while Safe is selected.
        var (model, root) = Model();
        var safe = Feature(model, root, "Safe");
        var experimental = Feature(model, root, "Experimental", selected: true);
        Constraint(model, root, FmConceptKeys.Excludes, safe, experimental);

        var result = Run(root);

        Assert.True(result.Succeeded);
        Assert.Equal(new StringValue("Experimental"), result.Value);
    }

    [Fact]
    public void AModelThatSelectsNothingSaysSo()
    {
        var (_, root) = Model();

        var result = Run(root);

        Assert.True(result.Succeeded);
        Assert.Equal(new StringValue("<nothing selected>"), result.Value);
    }

    [Fact]
    public void TheTraceShowsEveryRuleThatWasChecked()
    {
        var (model, root) = Model();
        var rule = Constraint(model, root, FmConceptKeys.Requires, Feature(model, root, "Core"), Feature(model, root, "Safe"));

        var result = Run(root);

        Assert.Equal(
            [FmConceptKeys.Requires, FmConceptKeys.Model],
            result.Trace.Select(entry => entry.ConceptKey));

        Assert.Equal(new BoolValue(true), result.Trace[0].Value);
        Assert.Equal(rule.Id, result.Trace[0].Node);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (Model Model, Node Root) Model()
    {
        var model = FeatureModelLanguage.NewProject().NewModel("Release");
        var root = model.NewNode(FmConceptKeys.Model);
        root.Set("name", "Release");
        model.AddRoot(root);

        return (model, root);
    }

    private static Node Feature(Model model, Node root, string name, bool selected = false)
    {
        var feature = model.NewChild(root, FeatureModelLanguage.FeaturesSlot, FmConceptKeys.Feature);
        feature.Set("name", name);
        feature.Set(FeatureModelLanguage.SelectedSlot, selected);

        return feature;
    }

    private static Node Constraint(Model model, Node root, string concept, Node subject, Node other)
    {
        var constraint = model.NewChild(root, FeatureModelLanguage.ConstraintsSlot, concept);
        constraint.SetReference(FeatureModelLanguage.SubjectSlot, subject);
        constraint.SetReference(FeatureModelLanguage.OtherSlot, other);

        return constraint;
    }

    private static InterpretationResult Run(Node root) =>
        new Interpreter(FeatureModelEvaluation.Registry).Run(root, cancellationToken: Token);
}
