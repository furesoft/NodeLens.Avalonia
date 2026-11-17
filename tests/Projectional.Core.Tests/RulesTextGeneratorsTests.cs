using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The two generators the sample language ships, pinned as text.</summary>
/// <remarks>
/// Golden output on purpose: generation must be byte-stable, so the assertion is the whole file rather than a
/// property of it. A change here is a change to what every consumer of the generated text sees.
/// </remarks>
public sealed class RulesTextGeneratorsTests
{
    [Fact]
    public void TheRuleTableAsJson()
    {
        var result = new Generator().Generate(PayrollSample.NewSampleModel().Roots[0], RulesTextGenerators.Json);

        Assert.True(result.Succeeded);
        Assert.Equal(
            """
            {
              "name": "Payroll",
              "rules": [
                { "rule": "Approved", "when": "(amount > 1000)", "outcome": "Accept" }
              ]
            }
            """,
            result.Text);
    }

    [Fact]
    public void TheRuleTableAsACSharpFragment()
    {
        var result = new Generator().Generate(PayrollSample.NewSampleModel().Roots[0], RulesTextGenerators.CSharp);

        Assert.True(result.Succeeded);
        Assert.Equal(
            """
            // Payroll
              if (amount > 1000) { return "Accept"; }
            """,
            result.Text);
    }

    [Fact]
    public void EveryGeneratorIsReachableByName()
    {
        Assert.Equal(["Json", "C Sharp"], RulesTextGenerators.All.Select(generator => generator.Name));
    }

    [Fact]
    public void EveryRegionBelongsToTheModelThatWasGenerated()
    {
        var model = PayrollSample.NewSampleModel();
        var nodes = model.Nodes.Select(node => node.Id).ToHashSet();

        var result = new Generator().Generate(model.Roots[0], RulesTextGenerators.Json);

        Assert.NotEmpty(result.Regions);
        Assert.All(result.Regions, region => Assert.Contains(region.Node, nodes));

        // The outcome of the rule is a property of the rule node, which is what "go to source" needs to find.
        var outcome = result.Text.IndexOf("Accept", StringComparison.Ordinal);
        var region = result.RegionAt(outcome);

        Assert.NotNull(region);
        Assert.Equal("outcome", region.Value.Slot);
        Assert.Equal(model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0].Id, region.Value.Node);
    }

    [Fact]
    public void TwoRunsProduceTheSameBytes()
    {
        var model = PayrollSample.NewSampleModel();
        var generator = new Generator();

        Assert.Equal(
            generator.Generate(model.Roots[0], RulesTextGenerators.Json).Text,
            generator.Generate(model.Roots[0], RulesTextGenerators.Json).Text);
    }
}
