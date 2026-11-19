using Projectional.Core;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Which nodes point at a node, in which order, and what a change does to the answer.</summary>
/// <remarks>
/// The reverse index exists for two callers - a rename that must rewrite every reference, and the find-usages
/// gesture - and both need the same three properties: a usage names its owner and its slot, the order is the
/// document's, and the answer follows a change without anybody remembering to tell the index.
/// </remarks>
public sealed class ReferenceIndexTests
{
    [Fact]
    public void AReferenceIsAUsageOfItsTarget()
    {
        var tag = TestLanguage.NewModel().NewNode(TestLanguage.Tag);
        tag.Model.AddRoot(tag);

        var rule = tag.Model.NewNode(TestLanguage.Rule);
        rule.Model.AddRoot(rule);
        rule.SetReference("tag", tag);

        var usage = Assert.Single(ReferenceIndex.For(tag.Model.Project).UsagesOf(tag));

        Assert.Equal(new ReferenceUsage(rule.Id, "tag", tag.Id), usage);
    }

    [Fact]
    public void AnUnsetReferenceIsNotAUsage()
    {
        var rule = TestEditor.BareRule();

        Assert.Empty(ReferenceIndex.For(rule.Model.Project).UsagesOf(rule));
    }

    [Fact]
    public void UsagesAreWalkedInDocumentOrder()
    {
        var project = TestLanguage.NewProject();
        var first = project.NewModel("First");
        var second = project.NewModel("Second");

        var tag = first.NewNode(TestLanguage.Tag);
        first.AddRoot(tag);

        var before = first.NewNode(TestLanguage.Rule);
        first.AddRoot(before);
        before.SetReference("tag", tag);

        var after = second.NewNode(TestLanguage.Rule);
        second.AddRoot(after);
        after.SetReference("tag", tag);

        var usages = ReferenceIndex.For(project).UsagesOf(tag);

        Assert.Equal([before.Id, after.Id], usages.Select(usage => usage.Owner));
    }

    [Fact]
    public void TheReferenceSlotsOfOneNodeFollowTheirDeclaration()
    {
        var target = TestEditor.BareRule();
        var owner = target.Model.NewNode(TestLanguage.Rule);
        owner.Model.AddRoot(owner);

        // `inherits` is declared before `self`, so a node using both is two usages in that order.
        owner.SetReference("inherits", target);
        owner.SetReference("self", target);

        var usages = ReferenceIndex.For(owner.Model.Project).UsagesOf(target);

        Assert.Equal(["inherits", "self"], usages.Select(usage => usage.Slot));
    }

    [Fact]
    public void ClearingAReferenceRemovesTheUsage()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();
        var index = ReferenceIndex.For(rule.Model.Project);

        Assert.Single(index.UsagesOf(tag));

        rule.SetReference("tag", null);

        Assert.Empty(index.UsagesOf(tag));
    }

    [Fact]
    public void PointingAReferenceElsewhereMovesTheUsage()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();
        var other = rule.Model.NewNode(TestLanguage.Tag);
        rule.Model.AddRoot(other);
        var index = ReferenceIndex.For(rule.Model.Project);

        rule.SetReference("tag", other);

        Assert.Empty(index.UsagesOf(tag));
        Assert.Single(index.UsagesOf(other));
    }

    [Fact]
    public void ANewReferenceIsSeenWithoutTheIndexBeingTold()
    {
        var (rule, tag) = TestEditor.RuleUsingTag();
        var index = ReferenceIndex.For(rule.Model.Project);

        Assert.Single(index.UsagesOf(tag));

        var later = rule.Model.NewNode(TestLanguage.Rule);
        rule.Model.AddRoot(later);
        later.SetReference("tag", tag);

        Assert.Equal(2, index.UsagesOf(tag).Count);
    }

    [Fact]
    public void ADetachedReferrerStopsBeingAUsage()
    {
        var target = TestEditor.BareRule();
        var host = target.Model.NewNode(TestLanguage.Rule);
        host.Model.AddRoot(host);

        var referrer = host.Model.NewChild(host, "extends", TestLanguage.Rule);
        referrer.SetReference("inherits", target);

        var index = ReferenceIndex.For(host.Model.Project);
        Assert.Single(index.UsagesOf(target));

        // Replacing a single child deletes the old one, which is the deletion path a usage has to survive.
        host.SetChild("extends", null);

        Assert.Empty(index.UsagesOf(target));
    }

    [Fact]
    public void AReferrerInAnotherModelCounts()
    {
        var project = TestLanguage.NewProject();
        var first = project.NewModel("First");
        var second = project.NewModel("Second");

        var target = first.NewNode(TestLanguage.Rule);
        first.AddRoot(target);

        var referrer = second.NewNode(TestLanguage.Rule);
        second.AddRoot(referrer);
        referrer.SetReference("inherits", target);

        Assert.Single(ReferenceIndex.For(project).UsagesOf(target));
    }

    [Fact]
    public void ANodeOfAnotherProjectIsRefused()
    {
        var mine = TestEditor.BareRule();
        var theirs = TestEditor.BareRule();

        Assert.Throws<ArgumentException>(
            () => ReferenceIndex.For(mine.Model.Project).UsagesOf(theirs));
    }

}
