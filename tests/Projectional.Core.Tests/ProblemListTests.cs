using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The problems panel's rows: their order, where each jumps to, and what each says it is about.</summary>
/// <remarks>
/// The order is the point of the type. A panel sorted by severity would renumber itself while the user fixes things,
/// so the rows follow the document and the severity travels with each row instead. The two ends of the list are pinned
/// as well: the first finding in the document is the first row, and a finding whose node has been deleted is still a
/// row - at the end, because there is nowhere to jump.
/// </remarks>
public sealed class ProblemListTests
{
    [Fact]
    public void TheRowsAreInDocumentOrder()
    {
        var (model, rule, steps) = Model();

        var rows = ProblemList.Of(
            model,
            [
                new Diagnostic(DiagnosticSeverity.Warning, steps[1].Id, "second"),
                new Diagnostic(DiagnosticSeverity.Hint, rule.Id, "first"),
            ]);

        Assert.Equal(["first", "second"], [.. rows.Select(row => row.Text.Split(" - ")[0])]);
    }

    [Fact]
    public void AFindingAboutASlotJumpsToThatSlot()
    {
        var (model, rule, _) = Model();

        var rows = ProblemList.Of(
            model,
            [new Diagnostic(DiagnosticSeverity.Warning, rule.Id, "named badly", role: "name")]);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), Assert.Single(rows).Place);
    }

    [Fact]
    public void AFindingWithoutASlotJumpsToItsNode()
    {
        var (model, rule, _) = Model();

        var rows = ProblemList.Of(model, [new Diagnostic(DiagnosticSeverity.Info, rule.Id, "whole node")]);

        Assert.Equal(new CellAddress(rule.Id), Assert.Single(rows).Place);
    }

    [Fact]
    public void AFindingAboutANodeThatIsGoneIsStillARow()
    {
        var (model, rule, _) = Model();

        var rows = ProblemList.Of(
            model,
            [
                new Diagnostic(DiagnosticSeverity.Error, NodeId.New(), "stale"),
                new Diagnostic(DiagnosticSeverity.Hint, rule.Id, "live"),
            ]);

        Assert.Equal(2, rows.Count);
        Assert.StartsWith("live", rows[0].Text, StringComparison.Ordinal);
        Assert.Contains("gone", rows[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ARowNamesTheNodeByNameAndFallsBackToItsConcept()
    {
        var (model, rule, steps) = Model();

        rule.Set("name", "Approved");

        var rows = ProblemList.Of(
            model,
            [
                new Diagnostic(DiagnosticSeverity.Hint, rule.Id, "named"),
                new Diagnostic(DiagnosticSeverity.Hint, steps[0].Id, "unnamed"),
            ]);

        Assert.Contains("Approved", rows[0].Text, StringComparison.Ordinal);
        Assert.Contains(TestLanguage.Step, rows[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSeverityTravelsWithItsRow()
    {
        var (model, rule, _) = Model();

        var rows = ProblemList.Of(model, [new Diagnostic(DiagnosticSeverity.Warning, rule.Id, "careful")]);

        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(rows).Severity);
        Assert.Equal("Warning", Assert.Single(rows).SeverityLabel);
    }

    [Fact]
    public void ChoosingARowPutsTheCaretOnWhatTheFindingIsAbout()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var row = Assert.Single(
            ProblemList.Of(rule.Model, [new Diagnostic(DiagnosticSeverity.Warning, rule.Id, "named badly", role: "name")]));

        // What the panel does with a chosen row, and the whole of what it does: the rest is scrolling.
        session.Select(Selection.At(row.Place));

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), session.Selection.Caret.Address);
    }

    /// <summary>A rule holding a body with two steps, which is enough document to order things in.</summary>
    private static (Model Model, Node Rule, IReadOnlyList<Node> Steps) Model()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var first = model.NewChild(body, "steps", TestLanguage.Step);
        var second = model.NewChild(body, "steps", TestLanguage.Step);

        return (model, rule, [first, second]);
    }
}