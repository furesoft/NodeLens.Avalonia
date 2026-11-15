using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The snapshot renderer is the only place where a whole notation meets a width, so these tests are
/// the regression net for notation as text: what a language author writes in a notation, drawn.
/// </summary>
public sealed class LayoutSnapshotTests
{
    [Fact]
    public void RendersARuleWithItsBodyAsAnIndentedBlock()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        rule.Set("name", "Payroll");

        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        step.Set("index", 1);
        step.Set("text", "Pay");
        model.NewChild(body, "notes", TestLanguage.Note).Set("text", "monthly");

        Assert.Equal(
            """
            rule Payroll as <none> {
              1 : Pay <empty>
              note monthly
              extends <empty>
            }
            """,
            LayoutSnapshot.Render(model, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void DrawsEmptySlotsAndEmptyCollectionsWithTheirPlaceholder()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.NewChild(rule, "body", TestLanguage.Body);

        Assert.Equal(
            """
            rule unnamed as <none> {
              <empty>
              <empty>
              extends <empty>
            }
            """,
            LayoutSnapshot.Render(model, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void SeparatesCollectionElementsWithTheNotationsSeparator()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        model.NewChild(body, "notes", TestLanguage.Note).Set("text", "a");
        model.NewChild(body, "notes", TestLanguage.Note).Set("text", "b");

        Assert.Equal(
            """
            <empty>
            note a; note b
            """,
            LayoutSnapshot.Render(model, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void CutsTextAtTheRequestedWidthInsteadOfWrappingIt()
    {
        var model = TestLanguage.NewModel();
        model.NewNode(TestLanguage.Rule);

        Assert.Equal(
            """
            rule u
              <emp
              exte
            }
            """,
            LayoutSnapshot.Render(model, TestNotations.Registry, width: 6));
    }

    [Fact]
    public void RendersOneNodeWithoutItsSiblings()
    {
        var model = TestLanguage.NewModel();
        model.NewNode(TestLanguage.Rule);
        var note = model.NewNode(TestLanguage.Note);
        note.Set("text", "monthly");

        Assert.Equal(
            "note monthly",
            LayoutSnapshot.Render(note, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void StacksRootsVerticallyWithNoGapBetweenThem()
    {
        var model = TestLanguage.NewModel();
        model.NewNode(TestLanguage.Note).Set("text", "first");
        model.NewNode(TestLanguage.Note).Set("text", "second");

        Assert.Equal(
            """
            note first
            note second
            """,
            LayoutSnapshot.Render(model, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void ShowsAMarkerForAConceptWithoutNotation()
    {
        var model = TestLanguage.NewModel();
        var tag = model.NewNode(TestLanguage.Tag);
        tag.Set("label", "urgent");

        Assert.Equal(
            $"<no notation for {TestLanguage.Tag}>",
            LayoutSnapshot.Render(tag, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void RendersAnEmptyModelAsNothing()
    {
        Assert.Equal(string.Empty, LayoutSnapshot.Render(TestLanguage.NewModel(), TestNotations.Registry, width: 40));
    }

    [Fact]
    public void RendersTheSameModelTheSameWayTwice()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        model.NewChild(body, "notes", TestLanguage.Note).Set("text", "stable");

        var first = LayoutSnapshot.Render(model, TestNotations.Registry, width: 40);

        Assert.Equal(first, LayoutSnapshot.Render(model, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void RefusesAModelWithoutRoom()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LayoutSnapshot.Render(TestLanguage.NewModel(), TestNotations.Registry, width: 0));
    }

    [Fact]
    public void RefusesAMissingModel()
    {
        Assert.Throws<ArgumentNullException>(
            () => LayoutSnapshot.Render((Model)null!, TestNotations.Registry, width: 40));
    }

    [Fact]
    public void RefusesAMissingNotationRegistry()
    {
        Assert.Throws<ArgumentNullException>(
            () => LayoutSnapshot.Render(TestLanguage.NewModel(), null!, width: 40));
    }
}
