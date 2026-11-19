using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Renaming a node, and what happens to the references that point at it.</summary>
/// <remarks>
/// A reference resolves by id, so the interesting half is the stored fallback text: it has to follow the name, in the
/// same undo step, and only where the model being edited can take the change back. The three answers - renamed,
/// unchanged, refused - are kept apart because a bar that reports them has to say something different about each.
/// </remarks>
public sealed class RenameNodeActionTests
{
    [Fact]
    public void ARenameWritesTheNameAndRefreshesEveryFallback()
    {
        var (rule, tag) = RuleWithNamedTag("Idle");
        var other = rule.Model.NewNode(TestLanguage.Rule);
        rule.Model.AddRoot(other);
        other.SetReference("tag", tag);

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new RenameNodeAction(tag.Id, "Waiting"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("Waiting", tag.Get("label"));
        Assert.Equal("Waiting", rule.GetReference("tag")!.Value.ResolveInfo);
        Assert.Equal("Waiting", other.GetReference("tag")!.Value.ResolveInfo);
    }

    [Fact]
    public void ARenameIsOneUndoStep()
    {
        var (rule, tag) = RuleWithNamedTag("Idle");

        using var session = TestEditor.Open(rule);
        var before = session.History.UndoDepth;

        session.Apply(new RenameNodeAction(tag.Id, "Waiting"));

        Assert.Equal(before + 1, session.History.UndoDepth);

        Assert.True(session.Undo());

        Assert.Equal("Idle", tag.Get("label"));
        Assert.Equal("Idle", rule.GetReference("tag")!.Value.ResolveInfo);
    }

    [Fact]
    public void RenamingToTheNameItAlreadyHasChangesNothing()
    {
        var (rule, tag) = RuleWithNamedTag("Idle");

        using var session = TestEditor.Open(rule);
        var before = session.History.UndoDepth;

        var result = session.Apply(new RenameNodeAction(tag.Id, "Idle"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(before, session.History.UndoDepth);
        Assert.Equal("Idle", rule.GetReference("tag")!.Value.ResolveInfo);
    }

    [Fact]
    public void AConceptWithoutANameSlotIsRefused()
    {
        var step = TestEditor.BareStep();

        using var session = TestEditor.Open(step);

        var result = session.Apply(new RenameNodeAction(step.Id, "anything"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Contains("name slot", result.Reason);
    }

    [Fact]
    public void ANodeThatIsGoneIsRefused()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new RenameNodeAction(NodeId.New(), "anything"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Contains("no longer holds", result.Reason);
    }

    [Fact]
    public void ANodeOfAnotherModelIsRefused()
    {
        var project = TestLanguage.NewProject();
        var here = project.NewModel("Here");
        var there = project.NewModel("There");

        var document = here.NewNode(TestLanguage.Rule);
        here.AddRoot(document);

        var other = there.NewNode(TestLanguage.Rule);
        there.AddRoot(other);

        using var session = TestEditor.Open(document);

        var result = session.Apply(new RenameNodeAction(other.Id, "Renamed"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Contains("another model", result.Reason);
        Assert.Equal("unnamed", other.Get("name"));
    }

    [Fact]
    public void AFallbackInAnotherModelIsLeftAlone()
    {
        var project = TestLanguage.NewProject();
        var first = project.NewModel("First");
        var second = project.NewModel("Second");

        var target = first.NewNode(TestLanguage.Rule);
        target.Set("name", "Old");
        first.AddRoot(target);

        var local = first.NewNode(TestLanguage.Rule);
        first.AddRoot(local);
        local.SetReference("inherits", target);

        var remote = second.NewNode(TestLanguage.Rule);
        second.AddRoot(remote);
        remote.SetReference("inherits", target);

        using var session = TestEditor.Open(target);

        session.Apply(new RenameNodeAction(target.Id, "New"));

        Assert.Equal("New", target.Get("name"));
        Assert.Equal("New", local.GetReference("inherits")!.Value.ResolveInfo);

        // The documented boundary: a second model has its own history, so the rename does not reach its fallback.
        Assert.Equal("Old", remote.GetReference("inherits")!.Value.ResolveInfo);
    }

    [Fact]
    public void TheCaretLandsOnTheNewName()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        session.Apply(new RenameNodeAction(rule.Id, "Renamed"));

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), session.Selection.Caret.Address);
        Assert.Equal("Renamed", rule.Get("name"));
    }

    [Fact]
    public void ARenameDoesNotTouchTheReferencesTheNodeItselfHolds()
    {
        var (rule, tag) = RuleWithNamedTag("Idle");
        var referrer = rule.Model.NewNode(TestLanguage.Rule);
        rule.Model.AddRoot(referrer);
        referrer.SetReference("inherits", rule);

        using var session = TestEditor.Open(rule);

        session.Apply(new RenameNodeAction(rule.Id, "Renamed"));

        // The rule's own reference still points at the tag, whose name did not change - so its fallback stays.
        Assert.Equal("Idle", rule.GetReference("tag")!.Value.ResolveInfo);

        // The reference that points at the renamed node follows the new name.
        Assert.Equal("Renamed", referrer.GetReference("inherits")!.Value.ResolveInfo);
    }

    private static (Node Rule, Node Tag) RuleWithNamedTag(string name)
    {
        var rule = TestEditor.BareRule();
        var tag = rule.Model.NewNode(TestLanguage.Tag);
        tag.Set("label", name);
        rule.Model.AddRoot(tag);
        rule.SetReference("tag", tag);

        return (rule, tag);
    }
}
