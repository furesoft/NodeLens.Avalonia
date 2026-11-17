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
}