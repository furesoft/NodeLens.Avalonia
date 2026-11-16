using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A finding is never invisible, which is the one rule folding has to add rather than reuse.</summary>
public sealed class FoldedMarkerTests
{
    [Fact]
    public void AFindingInsideAFoldedNodeMarksTheFoldedRow()
    {
        var (rule, body, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        session.ToggleCollapsed(rule.Id);

        // The step is inside the fold, so nothing in the layout carries it any more.
        var finding = new Diagnostic(DiagnosticSeverity.Error, step.Id, "the step is wrong", code: "PROBE001");

        var cells = MarkerCells.Of(session.Layout, finding);

        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.IsType<CollapsedCell>(cell));
        Assert.Equal(rule.Id, cells[0].Address.Owner);
    }

    [Fact]
    public void AnUnfoldedDocumentStillMarksTheNodeItself()
    {
        var (rule, body, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        var finding = new Diagnostic(DiagnosticSeverity.Error, step.Id, "the step is wrong", code: "PROBE001");

        var cells = MarkerCells.Of(session.Layout, finding);

        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.Equal(step.Id, cell.Address.Owner));
    }
}
