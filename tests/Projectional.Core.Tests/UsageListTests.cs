using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>How a usage report becomes the rows a panel reads.</summary>
/// <remarks>
/// The order is the document's, the text names what points at the target rather than an id, and a usage in another
/// model says which one - because a reader looking at a list cannot infer that from a row. The rows carry the model
/// so that choosing one can find the tab it belongs to.
/// </remarks>
public sealed class UsageListTests
{
    [Fact]
    public void AUsageBecomesARowNamingItsOwnerAndSlot()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();
        rule.Set("name", "First");

        var row = Assert.Single(UsageList.Of(FindUsages.Of(tag)));

        Assert.Same(rule.Model, row.Model);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "tag"), row.Place);
        Assert.Equal("Test.Rule \"First\" - tag", row.Text);
    }

    [Fact]
    public void AUsageInAnotherModelSaysWhichOne()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();
        rule.Set("name", "First");

        var other = rule.Model.Project.NewModel("Other");
        var second = other.NewNode(TestLanguage.Rule);
        second.Set("name", "Second");
        other.AddRoot(second);
        second.SetReference("tag", tag);

        var rows = UsageList.Of(FindUsages.Of(tag));

        Assert.Equal(
            ["Test.Rule \"First\" - tag", "Other: Test.Rule \"Second\" - tag"],
            rows.Select(row => row.Text));
        Assert.Same(other, rows[1].Model);
    }

    [Fact]
    public void AnOwnerThatIsGoneKeepsARow()
    {
        var tag = TestEditor.BareTag();
        tag.Set("label", "urgent");
        var report = new UsageReport(tag, [new UsageMatch(NodeId.New(), "tag", tag.Id)]);

        var row = Assert.Single(UsageList.Of(report));

        Assert.Same(tag.Model, row.Model);
        Assert.Equal("a node that is gone - tag", row.Text);
    }

    [Fact]
    public void AReportWithoutATargetHasNoRows()
    {
        Assert.Empty(UsageList.Of(UsageReport.None));
    }

    [Fact]
    public void AHeaderNamesTheConceptAndTheNameWhenThereIsOne()
    {
        var tag = TestEditor.BareTag();
        tag.Set("label", "urgent");

        Assert.Equal("Test.Tag \"urgent\"", UsageList.LabelOf(tag));
        Assert.Equal("Test.Tag", UsageList.LabelOf(TestEditor.BareTag()));
    }

    [Fact]
    public void AConceptWithoutANameSlotIsNamedByItsKey()
    {
        Assert.Equal("Test.Step", UsageList.LabelOf(TestEditor.BareStep()));
    }
}
