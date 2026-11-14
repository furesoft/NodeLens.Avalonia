using Xunit;

namespace Projectional.Core.Tests;

public sealed class ModelTransactionTests
{
    [Fact]
    public void ACommittedTransactionKeepsItsEditsAndPublishesThemOnce()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        var events = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => events.Add(e);

        using (var transaction = model.BeginTransaction())
        {
            node.Set("text", "a");
            node.Set("text", "b");
            transaction.Complete();
        }

        Assert.Equal("b", node.GetOrNull("text"));
        Assert.Single(events);
        Assert.Equal(2, events[0].Changes.Count);
    }

    [Fact]
    public void ATransactionDisposedWithoutCompletingIsRolledBack()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        var versionBefore = model.Version;
        var events = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => events.Add(e);

        using (model.BeginTransaction())
        {
            node.Set("text", "lost");
        }

        Assert.Null(node.GetOrNull("text"));
        Assert.Equal(versionBefore, model.Version);
        Assert.Empty(events);
    }

    [Fact]
    public void ARolledBackTransactionUndoesACreatedRoot()
    {
        var model = TestLanguage.NewModel();

        using (model.BeginTransaction())
        {
            model.NewNode(TestLanguage.Rule);
        }

        Assert.Empty(model.Roots);
        Assert.Equal(0, model.Project.NodeCount);
    }

    [Fact]
    public void ARolledBackTransactionUndoesACreatedChild()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        using (model.BeginTransaction())
        {
            model.NewChild(rule, "body", TestLanguage.Body);
        }

        Assert.Null(rule.GetChild("body"));
        Assert.Equal(1, model.Project.NodeCount);
    }

    [Fact]
    public void ARolledBackTransactionRestoresADeletedSubtreeInPlace()
    {
        var model = TestLanguage.NewModel();
        var first = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(first, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        var second = model.NewNode(TestLanguage.Rule);

        using (model.BeginTransaction())
        {
            first.RemoveChild("body", body);
        }

        Assert.NotNull(model.Project.FindNode(step.Id));
        Assert.Equal(4, model.Project.NodeCount);
        Assert.Same(first, body.Parent);
        Assert.Equal("body", body.ParentSlot);
        Assert.Equal([first, second], model.Roots);
        Assert.Equal([body], first.GetChildren("body"));
        Assert.Equal(0, body.IndexInParent);
    }

    [Fact]
    public void ARolledBackTransactionRestoresTheReferencesItCleared()
    {
        var model = TestLanguage.NewModel();
        var holder = model.NewNode(TestLanguage.Rule);
        var container = model.NewNode(TestLanguage.Rule);
        var extended = model.NewChild(container, "extends", TestLanguage.Rule);
        holder.SetReference("inherits", extended);

        using (model.BeginTransaction())
        {
            container.RemoveChild("extends", extended);
        }

        Assert.Equal(extended.Id, holder.GetReference("inherits")?.Target);
        Assert.Same(extended, holder.GetReferenceTarget("inherits"));
    }

    [Fact]
    public void ARolledBackTransactionPutsAMovedNodeBackWhereItWas()
    {
        var model = TestLanguage.NewModel();
        var first = model.NewNode(TestLanguage.Rule);
        var second = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(first, "body", TestLanguage.Body);

        using (model.BeginTransaction())
        {
            second.AddChild("body", body);
        }

        Assert.Same(first, body.Parent);
        Assert.Equal("body", body.ParentSlot);
        Assert.Null(second.GetChild("body"));
        Assert.Equal([body], first.GetChildren("body"));
    }

    [Fact]
    public void OnlyTheOutermostTransactionPublishes()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        var events = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => events.Add(e);

        using var outer = model.BeginTransaction();
        using (var inner = model.BeginTransaction())
        {
            node.Set("text", "a");
            inner.Complete();
        }

        Assert.Empty(events);
        outer.Complete();

        Assert.Single(events);
        Assert.Single(events[0].Changes);
    }

    [Fact]
    public void RollingBackAnInnerTransactionKeepsTheOuterEdits()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);

        using var outer = model.BeginTransaction();
        node.Set("text", "kept");

        using (model.BeginTransaction())
        {
            node.Set("text", "dropped");
        }

        Assert.Equal("kept", node.GetOrNull("text"));

        outer.Complete();
        Assert.Equal("kept", node.GetOrNull("text"));
    }

    [Fact]
    public void HasTransactionIsTrueUntilTheOutermostTransactionEnds()
    {
        var model = TestLanguage.NewModel();
        Assert.False(model.HasTransaction);

        using var outer = model.BeginTransaction();
        Assert.True(model.HasTransaction);

        using (var inner = model.BeginTransaction())
        {
            inner.Complete();
        }

        Assert.True(model.HasTransaction);
        outer.Complete();
        Assert.False(model.HasTransaction);
    }

    [Fact]
    public void CompletingATransactionTwiceThrows()
    {
        var model = TestLanguage.NewModel();
        var transaction = model.BeginTransaction();
        transaction.Complete();

        Assert.Throws<ObjectDisposedException>(transaction.Complete);
        transaction.Dispose();
    }

    [Fact]
    public void AFailedReplacementOfAChildLeavesTheOldChildStanding()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var tag = model.NewNode(TestLanguage.Tag);

        var versionBefore = model.Version;

        Assert.Throws<ModelInvariantException>(() => rule.SetChild("body", tag));

        Assert.Same(body, rule.GetChild("body"));
        Assert.NotNull(model.Project.FindNode(body.Id));
        Assert.Equal([rule, tag], model.Roots);
        Assert.Equal(versionBefore, model.Version);
    }
}
