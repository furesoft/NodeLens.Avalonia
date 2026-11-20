using Projectional.Core;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The model browser's rows: order, depth, and what a row is called.</summary>
/// <remarks>
/// The browser walks the model rather than the projection, so what is pinned here is that a document's structure is
/// described completely - order is document order and depth is counted from the root, because depth is a property of
/// where a node sits and a node can be moved.
/// </remarks>
public sealed class OutlineTests
{
    [Fact]
    public void TheRootComesFirstAndChildrenFollowDepthFirst()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var first = model.NewChild(body, "steps", TestLanguage.Step);
        var detail = model.NewChild(first, "detail", TestLanguage.Step);

        var rows = Outline.Of(model);

        Assert.Equal(
            [rule.Id, body.Id, first.Id, detail.Id],
            [.. rows.Select(row => row.Id)]);

        Assert.Equal([0, 1, 2, 3], [.. rows.Select(row => row.Depth)]);
    }

    [Fact]
    public void ARowNamesItsConceptAndItsName()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        rule.Set("name", "Approved");

        var step = model.NewChild(model.NewChild(rule, "body", TestLanguage.Body), "steps", TestLanguage.Step);

        var rows = Outline.Of(model);

        Assert.Equal("Rule \"Approved\"", rows[0].Text);
        Assert.Equal("Step", rows[^1].Text);
        Assert.Equal(step.Id, rows[^1].Id);
    }

    [Fact]
    public void TheRowIsIndentedByItsDepth()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);

        var rows = Outline.Of(model);

        Assert.Equal("Rule \"unnamed\"", rows[0].ToString());
        Assert.Equal("  Body", rows[1].ToString());
        Assert.Equal(body.Id, rows[1].Id);
    }

    [Fact]
    public void EveryRootIsListed()
    {
        var model = TestLanguage.NewModel();
        model.NewNode(TestLanguage.Rule);
        model.NewNode(TestLanguage.Tag);

        var rows = Outline.Of(model);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(0, row.Depth));
    }

    [Fact]
    public void ARowKnowsWhetherItCanBeOpened()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        model.NewChild(body, "steps", TestLanguage.Step);

        var rows = Outline.Of(model);

        Assert.True(rows[0].HasChildren);      // the rule has a body
        Assert.True(rows[0].IsExpanded);
        Assert.True(rows[1].HasChildren);      // the body has a step
        Assert.False(rows[^1].HasChildren);    // the step has nothing
        Assert.True(rows[^1].IsExpanded);
    }

    [Fact]
    public void ACollapsedNodeIsOneRowAndItsChildrenAreNotBuilt()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        model.NewChild(body, "steps", TestLanguage.Step);

        var rows = Outline.Of(model, new HashSet<NodeId> { rule.Id });

        var only = Assert.Single(rows);

        Assert.Equal(rule.Id, only.Id);
        Assert.True(only.HasChildren);
        Assert.False(only.IsExpanded);
    }

    [Fact]
    public void ACollapsedLeafIsAnOrdinaryRow()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        // Nothing is under it, so shutting it changes nothing - which is what keeps a leaf from drawing a chevron.
        var rows = Outline.Of(model, new HashSet<NodeId> { rule.Id });

        Assert.Single(rows);
        Assert.True(rows[0].IsExpanded);
        Assert.False(rows[0].HasChildren);
    }

    [Fact]
    public void EveryNodeWithChildrenIsWhatCollapseAllShuts()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);

        var nodes = Outline.NodesWithChildren(model);

        Assert.Contains(rule.Id, nodes);
        Assert.Contains(body.Id, nodes);
        Assert.DoesNotContain(step.Id, nodes);

        // Collapsing all of it leaves one row per root, which is the shape a reader asked for.
        var rows = Outline.Of(model, nodes);

        Assert.Single(rows);
        Assert.Equal(rule.Id, rows[0].Id);
    }

    [Fact]
    public void ARowIsDrawnAsTheConceptItIs()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        rule.Set("name", "Approved");

        var row = Outline.Of(model)[0];

        Assert.Equal(TestLanguage.Rule, row.Concept);
        Assert.Equal("R", row.Badge);
        Assert.Equal(0, row.Indent);
        Assert.False(row.HasIndent);
    }

    [Fact]
    public void AConceptKeepsItsColour()
    {
        // Stable across calls and across processes: a colour that changed between two runs would be a decoration. The
        // palette is small on purpose - it groups rows by concept, it does not identify one - so two concepts sharing a
        // colour is allowed and asserting otherwise would be asserting something the design does not promise.
        Assert.Same(OutlineIcons.BrushOf(TestLanguage.Rule), OutlineIcons.BrushOf(TestLanguage.Rule));
        Assert.Same(OutlineIcons.BrushOf("Rules.Rule"), OutlineIcons.BrushOf("Rules.Rule"));
    }

    [Fact]
    public void ABadgeIsTheConceptsInitialWithoutItsNamespace()
    {
        Assert.Equal("R", OutlineIcons.BadgeOf("Rules.Rule"));
        Assert.Equal("I", OutlineIcons.BadgeOf("Expressions.IntLiteral"));
        Assert.Equal("?", OutlineIcons.BadgeOf(null));
        Assert.Equal("?", OutlineIcons.BadgeOf(string.Empty));
    }
}