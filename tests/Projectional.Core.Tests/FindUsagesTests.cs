using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Which node the caret asks about, and who points at it.</summary>
/// <remarks>
/// The two readings are the feature: a caret on a reference asks about its target, a caret anywhere else asks about
/// the node whose cell it is in. The three answers - a target with usages, a target with none, no target at all - are
/// kept apart here because a panel has to say something different about each.
/// </remarks>
public sealed class FindUsagesTests
{
    [Fact]
    public void AReferenceCellAsksAboutItsTarget()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();

        using var session = TestEditor.Open(rule);
        var reference = Assert.Single(session.Tree.Cells().OfType<ReferenceCell>());
        session.Select(Selection.At(reference.Address));

        var report = FindUsages.ReportOf(session);

        Assert.Same(tag, report.Target);

        var usage = Assert.Single(report.Usages);
        Assert.Equal(rule.Id, usage.Owner);
        Assert.Equal("tag", usage.Slot);
        Assert.Equal(tag.Id, usage.Target);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "tag"), usage.Place);
    }

    [Fact]
    public void ACaretInsideANodeAsksAboutThatNode()
    {
        var rule = TestEditor.BareRule();

        // A second rule points at the first, and is not drawn: a usage does not have to be on the page the caret is on.
        var referrer = rule.Model.NewNode(TestLanguage.Rule);
        rule.Model.AddRoot(referrer);
        referrer.SetReference("inherits", rule);

        using var session = TestEditor.Open(rule);
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));

        var report = FindUsages.ReportOf(session);

        Assert.Same(rule, report.Target);
        Assert.Equal(CellAddress.OfSlot(referrer.Id, "inherits"), Assert.Single(report.Usages).Place);
    }

    [Fact]
    public void AnEmptyChildSlotAsksAboutTheNodeThatOwnsIt()
    {
        var (rule, _) = TestEditor.RuleWithBody();

        using var session = TestEditor.Open(rule);

        // `extends` is empty, so its cell carries its owner's slot address rather than a child's node address.
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "extends")));

        Assert.Same(rule, FindUsages.TargetOf(session));
    }

    [Fact]
    public void AnUnsetReferenceAsksAboutNothing()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);
        var reference = Assert.Single(session.Tree.Cells().OfType<ReferenceCell>());
        session.Select(Selection.At(reference.Address));

        var report = FindUsages.ReportOf(session);

        Assert.Null(report.Target);
        Assert.False(report.HasTarget);
        Assert.Empty(report.Usages);
    }

    [Fact]
    public void AnUnresolvedReferenceAsksAboutNothing()
    {
        var rule = TestEditor.BareRule();
        rule.SetReference("tag", NodeId.New(), resolveInfo: "gone");

        using var session = TestEditor.Open(rule);
        var reference = Assert.Single(session.Tree.Cells().OfType<ReferenceCell>());
        session.Select(Selection.At(reference.Address));

        Assert.Null(FindUsages.ReportOf(session).Target);
    }

    [Fact]
    public void ANodeNothingPointsAtHasATargetAndNoUsages()
    {
        var tag = TestEditor.BareTag();

        var report = FindUsages.Of(tag);

        Assert.Same(tag, report.Target);
        Assert.True(report.HasTarget);
        Assert.Empty(report.Usages);
    }

    [Fact]
    public void AReportFollowsTheModelWithoutBeingRefreshed()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();

        Assert.Single(FindUsages.Of(tag).Usages);

        rule.SetReference("tag", null);

        Assert.Empty(FindUsages.Of(tag).Usages);
    }

    [Fact]
    public void TheReportOfANodeIsTheSameAnswerTheCaretGives()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();

        using var session = TestEditor.Open(rule);
        var reference = Assert.Single(session.Tree.Cells().OfType<ReferenceCell>());
        session.Select(Selection.At(reference.Address));

        var fromCaret = FindUsages.ReportOf(session);
        var fromNode = FindUsages.Of(tag);

        Assert.Equal(fromNode.Usages, fromCaret.Usages);
    }
}
