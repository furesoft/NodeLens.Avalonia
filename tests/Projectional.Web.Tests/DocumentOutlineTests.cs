using Projectional.Core;

using Xunit;

namespace Projectional.Web.Tests;

public sealed class DocumentOutlineTests
{
    [Fact]
    public void ItWalksTheModelInDocumentOrderAndCountsTheDepth()
    {
        var model = WebLanguages.Rules.NewSample();

        var rows = DocumentOutline.Of(model, new HashSet<NodeId>());

        Assert.Equal(model.Nodes.Count(), rows.Count);
        Assert.Equal(0, rows[0].Depth);
        Assert.Equal("Ruleset \"Payroll\"", rows[0].Text);

        var rule = Assert.Single(rows, row => row.Text == "Rule \"Approved\"");
        Assert.Equal(1, rule.Depth);
        Assert.True(rule.HasChildren);
        Assert.True(rule.IsExpanded);
    }

    [Fact]
    public void ACollapsedNodeIsOneRowAndItsChildrenAreNotBuiltAtAll()
    {
        var model = WebLanguages.Rules.NewSample();
        var root = model.Roots[0];

        var rows = DocumentOutline.Of(model, new HashSet<NodeId> { root.Id });

        var only = Assert.Single(rows);
        Assert.Equal(root.Id, only.Id);
        Assert.True(only.HasChildren);
        Assert.False(only.IsExpanded);
    }

    [Fact]
    public void AncestorsAreNearestFirstAndDoNotContainTheNode()
    {
        var model = WebLanguages.Rules.NewSample();
        var rule = model.Nodes.First(node => node.Concept.Key == "Rules.Rule");

        var ancestors = DocumentOutline.AncestorsOf(model, rule.Id);

        Assert.Equal([model.Roots[0].Id], ancestors);
    }

    [Fact]
    public void NodesWithChildrenIsWhatCollapseAllShuts()
    {
        var model = WebLanguages.Rules.NewSample();

        var nodes = DocumentOutline.NodesWithChildren(model);

        Assert.Contains(model.Roots[0].Id, nodes);

        var leaf = model.Nodes.First(node => node.Concept.Key == "Expressions.IntLiteral");
        Assert.DoesNotContain(leaf.Id, nodes);
    }

    [Fact]
    public void AConceptWithoutANameIsSaidByItsShortName()
    {
        var model = WebLanguages.Rules.NewSample();

        var rows = DocumentOutline.Of(model, new HashSet<NodeId>());

        Assert.Contains(rows, row => row.Text == "Binary");
    }
}
