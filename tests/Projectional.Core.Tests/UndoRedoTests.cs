using Xunit;

namespace Projectional.Core.Tests;

public sealed class UndoRedoTests
{
    [Fact]
    public void AnUndonePropertySetGoesBackToItsPreviousValue()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        node.Set("text", "a");

        Assert.Equal(1, history.UndoDepth);
        Assert.True(history.Undo());
        Assert.Null(node.GetOrNull("text"));
        Assert.False(history.CanUndo);

        Assert.True(history.Redo());
        Assert.Equal("a", node.GetOrNull("text"));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoOfACreatedRootTakesItOutOfTheModelAndRedoPutsItBack()
    {
        var model = TestLanguage.NewModel();
        using var history = new UndoHistory(model);

        var node = model.NewNode(TestLanguage.Rule);
        Assert.Equal(1, history.UndoDepth);

        history.Undo();

        Assert.Empty(model.Roots);
        Assert.Equal(0, model.Project.NodeCount);
        Assert.Null(model.Project.FindNode(node.Id));

        history.Redo();

        Assert.Same(node, Assert.Single(model.Roots));
        Assert.True(node.IsRoot);
        Assert.Equal(0, node.IndexInParent);
        Assert.Equal(1, model.Project.NodeCount);
    }

    [Fact]
    public void UndoOfACreatedChildTakesItOutOfItsSlot()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        using var history = new UndoHistory(model);

        var body = model.NewChild(rule, "body", TestLanguage.Body);

        history.Undo();

        Assert.Null(rule.GetChild("body"));
        Assert.Equal(1, model.Project.NodeCount);

        history.Redo();

        Assert.Same(body, rule.GetChild("body"));
        Assert.Equal("body", body.ParentSlot);
        Assert.Equal(0, body.IndexInParent);
    }

    [Fact]
    public void UndoOfADeletionRestoresTheWholeSubtreeInPlace()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        var note = model.NewChild(body, "notes", TestLanguage.Note);
        using var history = new UndoHistory(model);

        rule.RemoveChild("body", body);

        Assert.Null(rule.GetChild("body"));
        Assert.Equal(1, model.Project.NodeCount);

        Assert.True(history.Undo());

        Assert.Same(body, rule.GetChild("body"));
        Assert.Equal("body", body.ParentSlot);
        Assert.Equal(0, body.IndexInParent);
        Assert.Same(step, Assert.Single(body.GetChildren("steps")));
        Assert.Same(note, Assert.Single(body.GetChildren("notes")));
        Assert.Equal(4, model.Project.NodeCount);
        Assert.Same(step, model.Project.FindNode(step.Id));
    }

    [Fact]
    public void UndoOfADeletionGivesBackTheReferencesItCleared()
    {
        var model = TestLanguage.NewModel();
        var root = model.NewNode(TestLanguage.Rule);
        var victim = model.NewChild(root, "extends", TestLanguage.Rule);
        var referrer = model.NewNode(TestLanguage.Rule);
        referrer.SetReference("inherits", victim);
        var before = Assert.IsType<NodeReference>(referrer.GetReference("inherits"));
        using var history = new UndoHistory(model);

        root.RemoveChild("extends", victim);

        Assert.Null(referrer.GetReference("inherits"));
        Assert.Null(referrer.GetReferenceTarget("inherits"));

        history.Undo();

        Assert.Equal(before, referrer.GetReference("inherits"));
        Assert.Same(victim, referrer.GetReferenceTarget("inherits"));

        history.Redo();

        Assert.Null(referrer.GetReference("inherits"));
        Assert.Equal(2, model.Project.NodeCount);
    }

    [Fact]
    public void UndoOfAMovePutsTheSubtreeBackWhereItCameFrom()
    {
        var model = TestLanguage.NewModel();
        var moving = model.NewNode(TestLanguage.Rule);
        var host = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(moving, "body", TestLanguage.Body);
        using var history = new UndoHistory(model);

        host.AddChild("extends", moving);

        Assert.Same(moving, host.GetChild("extends"));
        Assert.False(moving.IsRoot);
        Assert.Same(host, Assert.Single(model.Roots));

        history.Undo();

        Assert.Null(host.GetChild("extends"));
        Assert.True(moving.IsRoot);
        Assert.Same(moving, model.Roots[0]);
        Assert.Equal(0, moving.IndexInParent);
        Assert.Same(body, moving.GetChild("body"));

        history.Redo();

        Assert.Same(moving, host.GetChild("extends"));
        Assert.Same(host, Assert.Single(model.Roots));
        Assert.Equal(3, model.Project.NodeCount);
    }

    [Fact]
    public void UndoPublishesTheInverseRecordsAndRedoTheOriginalOnes()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);
        var events = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => events.Add(e);

        node.Set("text", "a");
        history.Undo();
        history.Redo();

        Assert.Equal(3, events.Count);
        Assert.Equal(new PropertyChanged(node.Id, "text", null, "a"), Assert.Single(events[0].Changes));
        Assert.Equal(new PropertyChanged(node.Id, "text", "a", null), Assert.Single(events[1].Changes));
        Assert.Equal(new PropertyChanged(node.Id, "text", null, "a"), Assert.Single(events[2].Changes));
    }

    [Fact]
    public void ARestoredDeletionIsReportedAsTheExactInverseOfTheRemoval()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        using var history = new UndoHistory(model);
        var events = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => events.Add(e);

        rule.RemoveChild("body", body);
        history.Undo();

        Assert.Equal(new NodeRemoved(body.Id, rule.Id, "body", 0), Assert.Single(events[0].Changes));
        Assert.Equal(new NodeAdded(body.Id, rule.Id, "body", 0), Assert.Single(events[1].Changes));
    }

    [Fact]
    public void TheHistoryKnowsWhatItCanUndoAndRedo()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);

        node.Set("text", "a");

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(1, history.UndoDepth);
        Assert.Equal(0, history.RedoDepth);

        history.Undo();

        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal(0, history.UndoDepth);
        Assert.Equal(1, history.RedoDepth);

        history.Redo();

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoAndRedoOnAnEmptyHistoryReportThatThereWasNothingToDo()
    {
        var model = TestLanguage.NewModel();
        using var history = new UndoHistory(model);

        Assert.False(history.Undo());
        Assert.False(history.Redo());
    }

    [Fact]
    public void ARestorationBumpsTheModelVersionLikeAnEditDoes()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        node.Set("text", "a");
        var afterEdit = model.Version;

        history.Undo();

        Assert.True(model.Version > afterEdit);

        var afterUndo = model.Version;
        history.Redo();

        Assert.True(model.Version > afterUndo);
    }

    [Fact]
    public void ConsecutiveEditsUnderTheSameKeyCollapseIntoOneUndoStep()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);
        history.CoalescingKey = "typing:step";

        node.Set("text", "a");
        node.Set("text", "ab");
        node.Set("text", "abc");

        Assert.Equal(1, history.UndoDepth);

        history.Undo();

        Assert.Null(node.GetOrNull("text"));

        history.Redo();

        Assert.Equal("abc", node.GetOrNull("text"));
    }

    [Fact]
    public void AnEditWithoutAKeyNeverJoinsThePreviousStep()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        node.Set("text", "a");
        node.Set("text", "ab");

        Assert.Equal(2, history.UndoDepth);
    }

    [Fact]
    public void ADifferentKeyStartsANewStep()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        history.CoalescingKey = "typing:one";
        node.Set("text", "a");

        history.CoalescingKey = "typing:two";
        node.Set("text", "ab");

        Assert.Equal(2, history.UndoDepth);
    }

    [Fact]
    public void ClearForgetsBothDirectionsAndTheCoalescingKey()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        node.Set("text", "a");
        node.Set("text", "ab");
        history.Undo();
        history.CoalescingKey = "typing:step";

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.UndoDepth);
        Assert.Equal(0, history.RedoDepth);
        Assert.Null(history.CoalescingKey);
    }

    [Fact]
    public void AnEditAfterAnUndoDropsTheRedoHistory()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        node.Set("text", "a");
        history.Undo();
        Assert.True(history.CanRedo);

        node.Set("text", "b");

        Assert.False(history.CanRedo);
        Assert.Equal(0, history.RedoDepth);
        Assert.Equal(1, history.UndoDepth);
    }

    [Fact]
    public void ANestedTransactionUndoesAsOneStep()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        using var history = new UndoHistory(model);

        using (var transaction = model.BeginTransaction())
        {
            node.Set("text", "a");
            node.Set("index", 3);
            transaction.Complete();
        }

        Assert.Equal(1, history.UndoDepth);

        history.Undo();

        Assert.Null(node.GetOrNull("text"));
        Assert.Null(node.GetOrNull("index"));
    }

    [Fact]
    public void DisposeStopsTheHistoryFromRecording()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);
        var history = new UndoHistory(model);

        history.Dispose();
        history.Dispose();

        node.Set("text", "a");

        Assert.False(history.CanUndo);
        Assert.Equal(0, history.UndoDepth);
    }
}
