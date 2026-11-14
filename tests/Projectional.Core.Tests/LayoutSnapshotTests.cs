using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The headless render: the text a model comes out as, before any notation exists to draw it.
/// </summary>
public sealed class LayoutSnapshotTests
{
    [Fact]
    public void AModelRendersAsAnIndentedOutlineOfItsNodes()
    {
        var model = TestLanguage.NewProject().NewModel("Payroll");

        var rule = model.NewNode(TestLanguage.Rule);
        rule.Set("name", "Payroll");

        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        step.Set("text", "Pay");
        var note = model.NewChild(body, "notes", TestLanguage.Note);
        note.Set("text", "monthly");

        Assert.Equal(
            """
            Test.Rule Payroll
              Test.Body
                Test.Step
                Test.Note
            """,
            LayoutSnapshot.Render(model, width: 40));
    }

    [Fact]
    public void ALineWiderThanTheSnapshotIsCutNotWrapped()
    {
        var model = TestLanguage.NewProject().NewModel();

        var rule = model.NewNode(TestLanguage.Rule);
        model.NewChild(rule, "body", TestLanguage.Body);

        Assert.Equal(
            """
            Test.R
              Test
            """,
            LayoutSnapshot.Render(model, width: 6));
    }

    [Fact]
    public void AnEmptyModelRendersNothing()
    {
        var model = TestLanguage.NewProject().NewModel();

        Assert.Equal("", LayoutSnapshot.Render(model, width: 20));
    }

    [Fact]
    public void TheSameModelRendersTheSameTextTwice()
    {
        var model = TestLanguage.NewProject().NewModel();

        var rule = model.NewNode(TestLanguage.Rule);
        rule.Set("name", "Twice");

        Assert.Equal(LayoutSnapshot.Render(model, width: 20), LayoutSnapshot.Render(model, width: 20));
    }

    [Fact]
    public void ANodeWithoutANameRendersOnlyItsConcept()
    {
        var model = TestLanguage.NewProject().NewModel();
        model.NewNode(TestLanguage.Step);

        Assert.Equal("Test.Step", LayoutSnapshot.Render(model, width: 20));
    }

    [Fact]
    public void AModelWithoutAWidthIsRefused()
    {
        var model = TestLanguage.NewProject().NewModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutSnapshot.Render(model, width: 0));
    }
}
