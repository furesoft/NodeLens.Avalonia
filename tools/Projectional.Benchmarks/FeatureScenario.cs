using Projectional.Checks;
using Projectional.Core;
using Projectional.Language;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.FeatureModel.Checks;
using Projectional.Semantics;

namespace Projectional.Benchmarks;

/// <summary>A feature model of <c>n</c> features and <c>n / 2</c> constraints between them.</summary>
/// <remarks>
/// Every constraint holds two references, so half the features' worth of extra nodes each carry two name lookups -
/// which is the shape that makes a document expensive for a reason its node count does not show. The ambiguity
/// constraint resolves a target's <em>current</em> name, so this is also the scenario that would have to be
/// rechecked somewhere else after a rename if checking were made incremental.
/// </remarks>
internal sealed class FeatureScenario : Scenario
{
    public override string Name => "features";

    public override IReadOnlyList<int> Sizes => [100, 500, 2_000, 5_000];

    public override ConstraintSet Constraints { get; } = ScopeConstraints.All;

    public override CheckSet Checks => FeatureModelChecks.All;

    public override TypeRegistry Types => TypeRegistry.Empty;

    public override NotationRegistry Notations { get; } =
        FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts);

    public override (Model Model, Node Root) Build(int units)
    {
        var model = FeatureModelLanguage.NewProject().NewModel("bench");
        var root = model.NewNode(FmConceptKeys.Model);
        model.AddRoot(root);
        root.Set("name", "Bench");

        var features = new List<Node>(units);

        for (var i = 1; i <= units; i++)
        {
            var feature = model.NewChild(root, FeatureModelLanguage.FeaturesSlot, FmConceptKeys.Feature);
            feature.Set("name", $"Feature{i}");
            features.Add(feature);
        }

        for (var i = 0; i < units / 2; i++)
        {
            var requires = model.NewChild(root, FeatureModelLanguage.ConstraintsSlot, FmConceptKeys.Requires);
            requires.SetReference(FeatureModelLanguage.SubjectSlot, features[i]);
            requires.SetReference(FeatureModelLanguage.OtherSlot, features[units - 1 - i]);
        }

        return (model, root);
    }

    /// <summary>The node holding the two references, because that is the unit an incremental caller would recheck.</summary>
    /// <remarks>
    /// A feature has no references of its own, so rechecking one would measure the cheap half only. The constraint
    /// node is what asks the scope twice, and each of those walks is the document's own size - which is the number
    /// this scenario exists for.
    /// </remarks>
    public override Node Representative(Node root) => root.GetChildren(FeatureModelLanguage.ConstraintsSlot)[0];

    public override Node EditTarget(Node root) => root.GetChildren(FeatureModelLanguage.FeaturesSlot)[^1];
}
