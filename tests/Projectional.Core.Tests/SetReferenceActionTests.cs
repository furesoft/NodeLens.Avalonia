using Projectional.Core;
using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The action that points a reference at a node, or clears it, by naming the owner and the slot.</summary>
/// <remarks>
/// The fixture's rule has three reference slots - <c>tag</c>, <c>inherits</c> and <c>self</c> - so a repair can be
/// tested for a target that fits, one that does not, and one that is in another document of the project.
/// </remarks>
public sealed class SetReferenceActionTests
{
    [Fact]
    public void ItPointsTheReferenceAtTheTarget()
    {
        var (rule, tag) = RuleAndTag();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetReferenceAction(rule.Id, "tag", tag.Id));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(tag.Id, rule.GetReferenceTarget("tag")?.Id);

        Assert.True(session.Undo());
        Assert.Null(rule.GetReference("tag"));
    }

    [Fact]
    public void ItClearsTheReference()
    {
        var (rule, tag) = RuleAndTag();
        rule.SetReference("tag", tag);

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetReferenceAction(rule.Id, "tag"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Null(rule.GetReference("tag"));

        Assert.True(session.Undo());
        Assert.Equal(tag.Id, rule.GetReferenceTarget("tag")?.Id);
    }

    [Fact]
    public void PointingAtTheTargetItAlreadyHasIsNotAnEdit()
    {
        var (rule, tag) = RuleAndTag();
        rule.SetReference("tag", tag);

        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new SetReferenceAction(rule.Id, "tag", tag.Id)).Outcome);
        Assert.False(session.Undo());
    }

    [Fact]
    public void ClearingAReferenceNobodySetIsNotAnEdit()
    {
        var (rule, _) = RuleAndTag();

        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new SetReferenceAction(rule.Id, "tag")).Outcome);
        Assert.False(session.Undo());
    }

    [Fact]
    public void ATargetInAnotherDocumentOfTheProjectIsAccepted()
    {
        // The model allows a reference across a model boundary and the scope offers the project's other documents
        // by name, so the edit layer must not be the place that refuses one - the SQL schema sample is built on it.
        var (rule, _) = RuleAndTag();
        var other = rule.Model.Project.NewModel("other");
        var tag = other.NewNode(TestLanguage.Tag);
        other.AddRoot(tag);

        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Applied, session.Apply(new SetReferenceAction(rule.Id, "tag", tag.Id)).Outcome);
        Assert.Equal(tag.Id, rule.GetReferenceTarget("tag")?.Id);
        Assert.Same(other, rule.GetReferenceTarget("tag")?.Model);
    }

    [Fact]
    public void ATargetInAnotherProjectIsRefused()
    {
        var (rule, _) = RuleAndTag();
        var foreign = TestLanguage.NewModel().NewNode(TestLanguage.Tag);
        foreign.Model.AddRoot(foreign);

        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Rejected, session.Apply(new SetReferenceAction(rule.Id, "tag", foreign.Id)).Outcome);
        Assert.Null(rule.GetReference("tag"));
    }

    [Fact]
    public void ATargetTheSlotDoesNotAcceptIsRefused()
    {
        var (rule, _) = RuleAndTag();
        var body = rule.Model.NewChild(rule, "body", TestLanguage.Body);

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetReferenceAction(rule.Id, "tag", body.Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("Body", result.Reason, StringComparison.Ordinal);
        Assert.Null(rule.GetReference("tag"));
    }

    [Fact]
    public void ASlotThatIsNotAReferenceIsRefused()
    {
        var (rule, _) = RuleAndTag();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetReferenceAction(rule.Id, "name", rule.Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("'name'", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeTheDocumentNoLongerHoldsIsRefused()
    {
        var (rule, tag) = RuleAndTag();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetReferenceAction(NodeId.New(), "tag", tag.Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("no longer holds", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ATargetThatIsGoneIsRefused()
    {
        var (rule, _) = RuleAndTag();

        using var session = TestEditor.Open(rule);

        var result = session.Apply(new SetReferenceAction(rule.Id, "tag", NodeId.New()));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("no longer in this project", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOwnerInAnotherDocumentIsRefused()
    {
        // The document being edited is the rule; a node of another model is not part of it, so this action cannot
        // write there - which is the same rule every other action follows.
        var (rule, _) = RuleAndTag();
        var other = rule.Model.Project.NewModel("other");
        var elsewhere = other.NewNode(TestLanguage.Rule);
        other.AddRoot(elsewhere);

        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Rejected, session.Apply(new SetReferenceAction(elsewhere.Id, "tag", rule.Id)).Outcome);
        Assert.Null(elsewhere.GetReference("tag"));
    }

    /// <summary>A rule and a tag beside it, each a root of one model.</summary>
    private static (Node Rule, Node Tag) RuleAndTag()
    {
        var rule = TestEditor.BareRule();
        var tag = rule.Model.NewNode(TestLanguage.Tag);

        tag.Set("label", "urgent");
        rule.Model.AddRoot(tag);

        return (rule, tag);
    }
}