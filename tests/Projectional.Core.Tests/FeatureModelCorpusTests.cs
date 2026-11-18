using System.Text;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Samples.FeatureModel;
using Projectional.Samples.FeatureModel.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The feature model sample's corpus document, and the three things it has to keep doing: save back
/// byte-identically, draw the way the notation says, and answer both as text and as a run.
/// </summary>
/// <remarks>
/// The generator's output is compared as text because that is what a consumer sees - the same rule the pilot's
/// generator tests follow - and the run's answer is the configuration the rules leave.
/// </remarks>
public sealed class FeatureModelCorpusTests
{
    private static readonly string CorpusDirectory = Path.Combine(AppContext.BaseDirectory, "FeatureModel");

    [Fact]
    public void TheSampleModelSavesBackByteIdentically()
    {
        var stored = File.ReadAllBytes(Path.Combine(CorpusDirectory, "release.mps"));

        var model = ModelXml.Deserialize(FeatureModelLanguage.NewProject(), Encoding.UTF8.GetString(stored));

        Assert.Equal(stored, Encoding.UTF8.GetBytes(ModelXml.Serialize(model)));
    }

    [Fact]
    public void TheSampleModelIsCleanUnderItsOwnChecks()
    {
        // The shipped document is what a user opens first, so a finding on it would greet them with a warning
        // about the sample itself - the same rule the pilot's corpus follows.
        Assert.Empty(FeatureModelChecks.All.Find(Load()));
    }

    [Fact]
    public void TheSampleModelIsDrawnAsAFeatureModel()
    {
        Assert.Equal(
            """
            model Release {
              features:
                feature Core selected
                feature Ui selected
                feature Safe selected
                feature Experimental
              constraints:
                Core requires Safe
                Ui requires Core
                Safe excludes Experimental
            }
            """,
            LayoutSnapshot.Render(
                Load().Roots[0],
                FeatureModelLanguage.CreateNotations(FeatureModelLanguage.Concepts),
                width: 100));
    }

    [Fact]
    public void TheSampleModelGeneratesTheConfigurationItSelects()
    {
        var generated = new Generator().Generate(Load().Roots[0], FeatureModelTextGenerators.Config);

        Assert.Equal(
            """
            Core=true
            Ui=true
            Safe=true
            Experimental=false
            """,
            generated.Text);
    }

    [Fact]
    public void TheSampleModelHoldsAndAnswersItsSelection()
    {
        var result = new Interpreter(FeatureModelEvaluation.Registry)
            .Run(Load().Roots[0], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(new StringValue("Core, Ui, Safe"), result.Value);
    }

    private static Core.Model Load() =>
        ModelXml.Deserialize(
            FeatureModelLanguage.NewProject(),
            File.ReadAllText(Path.Combine(CorpusDirectory, "release.mps")));
}
