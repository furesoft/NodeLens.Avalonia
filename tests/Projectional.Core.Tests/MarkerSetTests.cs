using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A marker set is the batch a check hands to the editor: it answers "what is wrong with this node"
/// without the caller scanning the whole list, and it never changes once it is built.
/// </summary>
/// <remarks>
/// The order assertions matter as much as the lookups. A problem list that reshuffles between two runs
/// of the same check looks like a second bug, so insertion order is part of the contract rather than an
/// accident of the dictionary.
/// </remarks>
public sealed class MarkerSetTests
{
    [Fact]
    public void TheEmptySetHoldsNothing()
    {
        var node = TestEditor.BareRule();

        Assert.Equal(0, MarkerSet.Empty.Count);
        Assert.Empty(MarkerSet.Empty.Diagnostics);
        Assert.Empty(MarkerSet.Empty.For(node.Id));
        Assert.Null(MarkerSet.Empty.SeverityOf(node.Id));
    }

    [Fact]
    public void DiagnosticsKeepTheOrderTheyWereBuiltWith()
    {
        var first = TestEditor.BareRule();
        var second = TestEditor.BareStep();

        var set = new MarkerSet(
            new Diagnostic(DiagnosticSeverity.Warning, second.Id, "second"),
            new Diagnostic(DiagnosticSeverity.Error, first.Id, "first"));

        Assert.Equal(["second", "first"], set.Diagnostics.Select(diagnostic => diagnostic.Message));
    }

    [Fact]
    public void ForAnswersAboutOneNodeOnly()
    {
        var rule = TestEditor.BareRule();
        var step = TestEditor.BareStep();

        var set = new MarkerSet(
            new Diagnostic(DiagnosticSeverity.Error, rule.Id, "rule one"),
            new Diagnostic(DiagnosticSeverity.Warning, step.Id, "step"),
            new Diagnostic(DiagnosticSeverity.Info, rule.Id, "rule two"));

        Assert.Equal(["rule one", "rule two"], set.For(rule.Id).Select(diagnostic => diagnostic.Message));
        Assert.Equal(["step"], set.For(step.Id).Select(diagnostic => diagnostic.Message));
    }

    [Fact]
    public void ForANodeWithNothingWrongIsEmpty()
    {
        // A host asks this for every cell it draws, so the healthy answer has to be cheap and non-null.
        var set = new MarkerSet(
            new Diagnostic(DiagnosticSeverity.Error, TestEditor.BareRule().Id, "elsewhere"));

        Assert.Empty(set.For(TestEditor.BareStep().Id));
    }

    [Fact]
    public void SeverityOfTakesTheWorstFinding()
    {
        var node = TestEditor.BareRule();

        var set = new MarkerSet(
            new Diagnostic(DiagnosticSeverity.Warning, node.Id, "warning"),
            new Diagnostic(DiagnosticSeverity.Error, node.Id, "error"),
            new Diagnostic(DiagnosticSeverity.Hint, node.Id, "hint"));

        Assert.Equal(DiagnosticSeverity.Error, set.SeverityOf(node.Id));
    }

    [Fact]
    public void SeverityOfANodeWithOneFindingIsThatFinding()
    {
        var node = TestEditor.BareRule();

        Assert.Equal(
            DiagnosticSeverity.Info,
            new MarkerSet(new Diagnostic(DiagnosticSeverity.Info, node.Id, "info")).SeverityOf(node.Id));
    }

    [Fact]
    public void SeverityOfANodeWithNothingWrongIsNull()
    {
        var set = new MarkerSet(
            new Diagnostic(DiagnosticSeverity.Error, TestEditor.BareRule().Id, "elsewhere"));

        Assert.Null(set.SeverityOf(TestEditor.BareStep().Id));
    }

    [Fact]
    public void TwoFindingsAboutOneNodeStayTwoFindings()
    {
        // Deduplicating would be the editor deciding that one check is more right than another.
        var node = TestEditor.BareRule();
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, node.Id, "same");

        var set = new MarkerSet(diagnostic, diagnostic);

        Assert.Equal(2, set.Count);
        Assert.Equal(2, set.For(node.Id).Count);
    }

    [Fact]
    public void ConcatKeepsBothOrders()
    {
        var rule = TestEditor.BareRule();
        var step = TestEditor.BareStep();

        var set = new MarkerSet(new Diagnostic(DiagnosticSeverity.Error, rule.Id, "one"))
            .Concat(new MarkerSet(
                new Diagnostic(DiagnosticSeverity.Warning, step.Id, "two"),
                new Diagnostic(DiagnosticSeverity.Info, rule.Id, "three")));

        Assert.Equal(["one", "two", "three"], set.Diagnostics.Select(diagnostic => diagnostic.Message));
        Assert.Equal(["one", "three"], set.For(rule.Id).Select(diagnostic => diagnostic.Message));
    }

    [Fact]
    public void ConcatWithNothingReturnsTheOtherSet()
    {
        // Joining a language's checks with a host's that found nothing must not copy the batch.
        var set = new MarkerSet(new Diagnostic(DiagnosticSeverity.Error, TestEditor.BareRule().Id, "one"));

        Assert.Same(set, set.Concat(MarkerSet.Empty));
        Assert.Same(set, MarkerSet.Empty.Concat(set));
    }

    [Fact]
    public void ANullDiagnosticIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new MarkerSet([null!]));
    }

    [Fact]
    public void ConcatRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => MarkerSet.Empty.Concat(null!));
    }
}
