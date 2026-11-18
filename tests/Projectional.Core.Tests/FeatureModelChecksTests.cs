using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.FeatureModel.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The feature model's own check: two features under one name.
/// </summary>
/// <remarks>
/// A check and not a constraint, because there is a repair - the later duplicate is renamed to the first free
/// suffix. The tests assert both halves: the finding, and that the repair is what removes it, since a fix that
/// leaves its own finding behind is broken.
/// </remarks>
public sealed class FeatureModelChecksTests
{
    [Fact]
    public void TwoFeaturesWithOneNameAreReported()
    {
        var (model, root) = Model();
        Feature(model, root, "Core");
        var second = Feature(model, root, "Core");

        var finding = Assert.Single(new FeatureNamesCheck().Find(model));

        Assert.Equal(second.Id, finding.Diagnostic.Target);
        Assert.Equal(FeatureNamesCheck.Code, finding.Diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Diagnostic.Severity);
        Assert.Equal("name", finding.Diagnostic.Role);
        Assert.Contains("Core", finding.Diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameThatIsAlreadyTakenTwiceGetsTheNextFreeSuffix()
    {
        var (model, root) = Model();
        Feature(model, root, "Core");
        Feature(model, root, "Core2");
        Feature(model, root, "Core");

        var findings = new FeatureNamesCheck().Find(model);

        // Core2 is taken, so the second duplicate cannot become Core2 - the label names what the repair will do.
        Assert.Equal("Rename to \"Core3\"", Assert.Single(findings).Label);
    }

    [Fact]
    public void DistinctNamesAreQuiet()
    {
        var (model, root) = Model();
        Feature(model, root, "Core");
        Feature(model, root, "Ui");

        Assert.Empty(new FeatureNamesCheck().Find(model));
    }

    [Fact]
    public void TheRepairRemovesItsOwnFinding()
    {
        var (model, root) = Model();
        Feature(model, root, "Core");
        var second = Feature(model, root, "Core");
        var fix = Assert.Single(new FeatureNamesCheck().Find(model));

        using var session = new EditorSession(root, FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts));
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("Core2", second.GetOrNull("name"));
        Assert.Empty(new FeatureNamesCheck().Find(model));
    }

    [Fact]
    public void TheSetIsTheStandardCataloguePlusThisCheck()
    {
        var all = FeatureModelChecks.All;

        Assert.Equal(StandardChecks.All.Count + 1, all.Count);
        Assert.IsType<FeatureNamesCheck>(all.Checks[^1]);
    }

    private static (Model Model, Node Root) Model()
    {
        var model = FeatureModelLanguage.NewProject().NewModel("Release");
        var root = model.NewNode(FmConceptKeys.Model);
        root.Set("name", "Release");
        model.AddRoot(root);

        return (model, root);
    }

    private static Node Feature(Model model, Node root, string name)
    {
        var feature = model.NewChild(root, FeatureModelLanguage.FeaturesSlot, FmConceptKeys.Feature);
        feature.Set("name", name);

        return feature;
    }
}
