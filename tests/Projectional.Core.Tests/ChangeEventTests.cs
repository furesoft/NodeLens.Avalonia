using Xunit;

namespace Projectional.Core.Tests;

public sealed class ChangeEventTests
{
    [Fact]
    public void AWriteIsPublishedWithTheModelIdAndTheVersionAfterTheEdit()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);

        var events = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => events.Add(e);

        node.Set("text", "hello");

        Assert.Single(events);
        Assert.Equal(model.Id, events[0].Model);
        Assert.Equal(model.Version, events[0].Version);
    }

    [Fact]
    public void APropertyChangeCarriesTheSlotNameAndBothValues()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);

        var change = Assert.IsType<PropertyChanged>(Single(model, () => node.Set("text", "hello")));

        Assert.Equal(node.Id, change.Target);
        Assert.Equal("text", change.Name);
        Assert.Null(change.Old);
        Assert.Equal("hello", change.New);
    }

    [Fact]
    public void WritingAPropertyThatAlreadyHoldsTheValuePublishesNothing()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Rule);

        var events = new List<ModelChangedEventArgs>();
        model.Changed += (_, e) => events.Add(e);

        node.Set("name", "unnamed");

        Assert.Empty(events);
    }

    [Fact]
    public void CreatingARootReportsOneAdditionWithoutParentOrRole()
    {
        var model = TestLanguage.NewModel();

        Node? created = null;
        var change = Assert.IsType<NodeAdded>(
            Single(model, () => created = model.NewNode(TestLanguage.Rule)));

        Assert.Equal(created!.Id, change.Target);
        Assert.Null(change.Parent);
        Assert.Null(change.Role);
        Assert.Equal(0, change.Index);
    }

    [Fact]
    public void CreatingAChildReportsOnlyWhereItWasPlaced()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);

        Node? created = null;
        var change = Assert.IsType<NodeAdded>(
            Single(model, () => created = model.NewChild(rule, "body", TestLanguage.Body)));

        Assert.Equal(created!.Id, change.Target);
        Assert.Equal(rule.Id, change.Parent);
        Assert.Equal("body", change.Role);
        Assert.Equal(0, change.Index);
    }

    [Fact]
    public void MovingANodeReportsRemovalFirstAndAdditionSecond()
    {
        var model = TestLanguage.NewModel();
        var first = model.NewNode(TestLanguage.Rule);
        var second = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(first, "body", TestLanguage.Body);

        var changes = Capture(model, () => second.AddChild("body", body));

        Assert.Collection(
            changes,
            change =>
            {
                var removed = Assert.IsType<NodeRemoved>(change);
                Assert.Equal(body.Id, removed.Target);
                Assert.Equal(first.Id, removed.Parent);
                Assert.Equal("body", removed.Role);
            },
            change =>
            {
                var added = Assert.IsType<NodeAdded>(change);
                Assert.Equal(body.Id, added.Target);
                Assert.Equal(second.Id, added.Parent);
                Assert.Equal("body", added.Role);
            });
    }

    [Fact]
    public void DeletingASubtreeReportsOneRemovalForTheWholeTree()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        model.NewChild(body, "steps", TestLanguage.Step);

        var change = Assert.IsType<NodeRemoved>(Single(model, () => rule.RemoveChild("body", body)));

        Assert.Equal(body.Id, change.Target);
        Assert.Equal(rule.Id, change.Parent);
        Assert.Equal("body", change.Role);
        Assert.Equal(0, change.Index);
    }

    [Fact]
    public void DeletingASubtreeAlsoReportsTheReferencesItCleared()
    {
        var model = TestLanguage.NewModel();
        var holder = model.NewNode(TestLanguage.Rule);
        var container = model.NewNode(TestLanguage.Rule);
        var extended = model.NewChild(container, "extends", TestLanguage.Rule);
        holder.SetReference("inherits", extended);

        var changes = Capture(model, () => container.RemoveChild("extends", extended));

        Assert.Collection(
            changes,
            change => Assert.Equal(extended.Id, Assert.IsType<NodeRemoved>(change).Target),
            change =>
            {
                var reference = Assert.IsType<ReferenceChanged>(change);
                Assert.Equal(holder.Id, reference.Target);
                Assert.Equal("inherits", reference.Name);
                Assert.Equal(extended.Id, reference.Old);
                Assert.Null(reference.New);
                Assert.True(reference.WasResolved);
            });
    }

    [Fact]
    public void SettingAReferenceReportsTargetsRatherThanNames()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);

        var change = Assert.IsType<ReferenceChanged>(Single(model, () => rule.SetReference("tag", tag)));

        Assert.Equal(rule.Id, change.Target);
        Assert.Equal("tag", change.Name);
        Assert.Null(change.Old);
        Assert.Equal(tag.Id, change.New);
        Assert.False(change.WasResolved);
    }

    [Fact]
    public void ClearingAReferenceReportsTheTargetItReleased()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);
        rule.SetReference("tag", tag);

        var change = Assert.IsType<ReferenceChanged>(Single(model, () => rule.SetReference("tag", null)));

        Assert.Equal(tag.Id, change.Old);
        Assert.Null(change.New);
        Assert.True(change.WasResolved);
    }

    [Fact]
    public void AChangeSetIsPublishedAfterTheEditsItDescribesAreVisible()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);

        string? observed = null;
        model.Changed += (_, _) => observed = node.GetOrNull("text");

        node.Set("text", "hello");

        Assert.Equal("hello", observed);
    }

    [Fact]
    public void ChangesArriveInTheOrderTheEditsHappened()
    {
        var model = TestLanguage.NewModel();
        var node = model.NewNode(TestLanguage.Step);

        var changes = Capture(model, () =>
        {
            node.Set("text", "a");
            node.Set("text", "b");
            node.Set("index", 3);
        });

        Assert.Collection(
            changes,
            change => Assert.Equal("a", Assert.IsType<PropertyChanged>(change).New),
            change => Assert.Equal("b", Assert.IsType<PropertyChanged>(change).New),
            change => Assert.Equal("index", Assert.IsType<PropertyChanged>(change).Name));
    }

    private static IModelChange Single(Model model, Action action)
    {
        var changes = Capture(model, action);
        return Assert.Single(changes);
    }

    private static List<IModelChange> Capture(Model model, Action action)
    {
        var events = new List<ModelChangedEventArgs>();
        void Handler(object? sender, ModelChangedEventArgs e) => events.Add(e);

        model.Changed += Handler;
        try
        {
            action();
        }
        finally
        {
            model.Changed -= Handler;
        }

        var changes = new List<IModelChange>();
        foreach (var batch in events)
        {
            changes.AddRange(batch.Changes);
        }

        return changes;
    }
}
