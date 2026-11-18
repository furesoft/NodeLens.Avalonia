using Projectional.Core;
using Projectional.Projection;
using Projectional.Samples.FeatureModel;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// How a feature model is drawn: a block of features, then a block of the rules between them.
/// </summary>
/// <remarks>
/// The selection is the interesting part of the drawing: `isSelected` is a conditional cell, so a feature that is
/// not selected shows its name and nothing else, and the snapshot says exactly that.
/// </remarks>
public sealed class FeatureModelNotationTests
{
    [Fact]
    public void AModelIsABracedBlockOfFeaturesAndConstraints()
    {
        var (model, root) = Model();
        var core = Feature(model, root, "Core", selected: true);
        Feature(model, root, "Ui");
        Constraint(model, root, FmConceptKeys.Requires, core);

        Assert.Equal(
            """
            model Release {
              features:
                feature Core selected
                feature Ui
              constraints:
                Core requires <none>
            }
            """,
            LayoutSnapshot.Render(root, FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts), width: 100));
    }

    [Fact]
    public void AFeatureThatIsNotSelectedDrawsNoSelectedWord()
    {
        var (model, root) = Model();
        var ui = Feature(model, root, "Ui");

        // The conditional cell emits no cells at all, rather than an empty one a caret could stop in.
        Assert.Equal(
            "feature Ui",
            LayoutSnapshot.Render(ui, FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts), width: 40));
    }

    [Fact]
    public void TheTwoKindsOfConstraintDifferInTheirWord()
    {
        var (model, root) = Model();
        var core = Feature(model, root, "Core", selected: true);
        var experimental = Feature(model, root, "Experimental");
        Constraint(model, root, FmConceptKeys.Excludes, core, experimental);

        Assert.Equal(
            """
            model Release {
              features:
                feature Core selected
                feature Experimental
              constraints:
                Core excludes Experimental
            }
            """,
            LayoutSnapshot.Render(root, FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts), width: 100));
    }

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

    private static void Constraint(Model model, Node root, string concept, Node subject, Node? other = null)
    {
        var constraint = model.NewChild(root, FeatureModelLanguage.ConstraintsSlot, concept);

        constraint.SetReference(FeatureModelLanguage.SubjectSlot, subject);

        if (other is not null)
        {
            constraint.SetReference(FeatureModelLanguage.OtherSlot, other);
        }
    }
}
