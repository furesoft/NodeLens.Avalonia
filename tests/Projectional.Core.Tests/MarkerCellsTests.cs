using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A diagnostic names a node, a slot and an index; a marker has to land on cells. This is the mapping
/// between the two, and it is the only place that knows it.
/// </summary>
/// <remarks>
/// The assertions are about which cells come back, not about how many characters they cover: the count
/// follows the notation, so a test that pinned it would break every time a keyword was added. What has
/// to stay true is that a finding is never invisible, never marks a node it is not about, and never
/// throws because the model moved on.
/// </remarks>
public sealed class MarkerCellsTests
{
    [Fact]
    public void AFindingAboutANodeMarksItsOwnText()
    {
        var rule = TestEditor.BareRule();

        var cells = CellsOf(TestEditor.Open(rule), new Diagnostic(DiagnosticSeverity.Error, rule.Id, "broken"));

        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.Equal(rule.Id, cell.Address.Owner));
        Assert.All(cells, cell => Assert.False(cell.Address.IsElement));
        Assert.Contains(cells, cell => CellText.Of(cell) == "rule");
        Assert.Contains(cells, cell => CellText.Of(cell) == "unnamed");
    }

    [Fact]
    public void AFindingAboutANodeStopsAtItsOwnSlots()
    {
        // The body's steps are a different node. Marking them from the body would make one finding on a
        // parent look like a finding on everything below it.
        var (body, steps) = TestEditor.BodyWithSteps("walk");

        var cells = CellsOf(TestEditor.Open(body), new Diagnostic(DiagnosticSeverity.Error, body.Id, "broken"));

        var cell = Assert.Single(cells);
        Assert.Equal("notes", cell.Address.Slot);
        Assert.DoesNotContain(cells, marked => marked.Address.Owner == steps[0].Id);
    }

    [Fact]
    public void AFindingAboutAPropertyMarksThatProperty()
    {
        var rule = TestEditor.BareRule();

        var cells = CellsOf(
            TestEditor.Open(rule),
            new Diagnostic(DiagnosticSeverity.Warning, rule.Id, "bad name", role: "name"));

        var cell = Assert.Single(cells);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), cell.Address);
        Assert.Equal("unnamed", CellText.Of(cell));
    }

    [Fact]
    public void AFindingAboutAChildSlotMarksTheChild()
    {
        var (rule, body) = TestEditor.RuleWithBody();

        var cells = CellsOf(
            TestEditor.Open(rule),
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "bad body", role: "body"));

        Assert.NotEmpty(cells);
        Assert.Contains(cells, cell => cell.Address.Owner == body.Id);
        Assert.DoesNotContain(cells, cell => CellText.Of(cell) == "rule");
    }

    [Fact]
    public void ARangeNarrowsAChildSlotToItsOwnCell()
    {
        // A range counts from the start of one value, so it cannot mean anything below the cell that
        // shows that value.
        var (rule, _) = TestEditor.RuleWithBody();

        var cells = CellsOf(
            TestEditor.Open(rule),
            new Diagnostic(
                DiagnosticSeverity.Error,
                rule.Id,
                "bad body",
                role: "body",
                range: CharacterRange.Of(0, 1)));

        var cell = Assert.Single(cells);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "body"), cell.Address);
    }

    [Fact]
    public void AnIndexMarksOneElementOfAList()
    {
        var (body, _) = TestEditor.BodyWithSteps("walk", "run");

        var cells = CellsOf(
            TestEditor.Open(body),
            new Diagnostic(DiagnosticSeverity.Error, body.Id, "bad step", role: "steps", index: 1));

        Assert.Contains(cells, cell => CellText.Of(cell) == "run");
        Assert.DoesNotContain(cells, cell => CellText.Of(cell) == "walk");
    }

    [Fact]
    public void ASlotTheNotationDoesNotShowFallsBackToTheNode()
    {
        // A variant may hide a slot, and a check runs against the model rather than against a drawing, so
        // an address that resolves to nothing must still produce a marker.
        var rule = TestEditor.BareRule();

        var cells = CellsOf(
            TestEditor.Open(rule),
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "bad", role: "no-such-slot"));

        Assert.Contains(cells, cell => CellText.Of(cell) == "rule");
    }

    [Fact]
    public void AFindingAboutANodeThatIsNotShownMarksNothing()
    {
        // The set was computed before the edit that removed the node. Dropping the marker is the only
        // honest answer - inventing a cell would put the mark somewhere it does not belong.
        var rule = TestEditor.BareRule();

        var cells = CellsOf(
            TestEditor.Open(rule),
            new Diagnostic(DiagnosticSeverity.Error, TestEditor.BareStep().Id, "stale"));

        Assert.Empty(cells);
    }

    [Fact]
    public void ANodeWithNoTextOfItsOwnMarksItsContainer()
    {
        // A concept whose notation is only a child slot draws no text at all once the child is filled,
        // and a finding about it would otherwise be invisible.
        var notations = new NotationRegistry(TestLanguage.Registry);
        notations.Add(TestLanguage.Rule, ed => ed.Sequence(ed.Line(ed.ChildInline("body"))));

        var (rule, _) = TestEditor.RuleWithBody();
        var cells = MarkerCells.Of(
            new EditorSession(rule, notations).Layout,
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "broken"));

        var cell = Assert.Single(cells);
        Assert.Equal(string.Empty, CellText.Of(cell));
        Assert.Equal(rule.Id, cell.Address.Owner);
    }

    [Fact]
    public void ARangeWithoutARoleIsIgnored()
    {
        // A range counts from the start of one value, and a node has no single value to count from.
        var rule = TestEditor.BareRule();
        var session = TestEditor.Open(rule);

        var withoutRange = CellsOf(session, new Diagnostic(DiagnosticSeverity.Error, rule.Id, "broken"));
        var withRange = CellsOf(
            session,
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "broken", range: CharacterRange.Of(0, 1)));

        Assert.Equal(withoutRange, withRange);
    }

    [Fact]
    public void ALayoutAndADiagnosticAreRequired()
    {
        var rule = TestEditor.BareRule();
        var layout = TestEditor.Open(rule).Layout;

        Assert.Throws<ArgumentNullException>(() => MarkerCells.Of(null!, new Diagnostic(DiagnosticSeverity.Error, rule.Id, "x")));
        Assert.Throws<ArgumentNullException>(() => MarkerCells.Of(layout, null!));
    }

    private static IReadOnlyList<EditorCell> CellsOf(EditorSession session, Diagnostic diagnostic) =>
        MarkerCells.Of(session.Layout, diagnostic);
}
