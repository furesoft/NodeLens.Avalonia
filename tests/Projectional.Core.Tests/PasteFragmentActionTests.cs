using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Pasting a fragment creates the nodes it holds, with new identities, where the caret says they go.
/// </summary>
/// <remarks>
/// The three places a paste can land are the three cases pinned here: an empty slot takes the fragment, a node in
/// a list takes it directly after itself, and a node in a single-valued slot has no room and is told so. The
/// other rules are about what the copies keep: values, children, and references - a reference between two copied
/// nodes has to point at the copy, while one that pointed outside keeps what it knew and degrades to unresolved
/// in a model where the target does not exist.
/// </remarks>
public sealed class PasteFragmentActionTests
{
    [Fact]
    public void PastingIntoAnEmptySlotCreatesCopiesWithNewIdentities()
    {
        var (source, steps) = TestEditor.BodyWithSteps("first", "second");
        var clipboard = new InMemoryClipboard();

        using (var copying = TestEditor.Open(source, clipboard))
        {
            TestEditor.SelectRange(copying, steps[0], steps[1]);
            copying.Apply(KeyRouter.For(copying, EditorKey.Copy));
        }

        var (target, _) = TestEditor.BodyWithSteps();
        using var session = TestEditor.Open(target, clipboard);

        session.Select(Selection.At(CellAddress.OfSlot(target.Id, "steps")));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Paste));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var pasted = target.GetChildren("steps");

        Assert.Equal(["first", "second"], pasted.Select(step => step.GetOrNull("text")));
        Assert.DoesNotContain(pasted[0].Id, steps.Select(original => original.Id));
        Assert.Equal(pasted[0].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void PastingBesideANodeInAListPutsTheFragmentDirectlyAfterIt()
    {
        var (source, copied) = TestEditor.BodyWithSteps("first");
        var (target, existing) = TestEditor.BodyWithSteps("one", "two");
        var clipboard = new InMemoryClipboard { Fragment = ModelFragment.Of(copied) };
        using var session = TestEditor.Open(target, clipboard);

        session.Select(Selection.At(CellAddress.OfSlot(existing[0].Id, "text")));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Paste));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var steps = target.GetChildren("steps");

        Assert.Equal(3, steps.Count);
        Assert.Same(existing[0], steps[0]);
        Assert.Same(existing[1], steps[2]);
        Assert.Equal("first", steps[1].GetOrNull("text"));
        Assert.Equal(source.GetChildren("steps")[0].Id, copied[0].Id);
    }

    [Fact]
    public void PastingBesideAFilledSingleSlotIsRefusedWithAReason()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        var inner = rule.Model.NewChild(rule, "extends", TestLanguage.Rule);
        var clipboard = new InMemoryClipboard { Fragment = ModelFragment.Of([TestEditor.BareRule()]) };
        using var session = TestEditor.Open(rule, clipboard);

        // A caret inside the only child of a single-valued slot: there is no "next to it" to paste at.
        session.Select(Selection.At(CellAddress.OfSlot(inner.Id, "name")));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Paste));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("holds one child", result.Reason, StringComparison.Ordinal);
        Assert.Same(inner, rule.GetChild("extends"));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void PastingOnARootIsRefused()
    {
        var rule = TestEditor.BareRule();
        var clipboard = new InMemoryClipboard { Fragment = ModelFragment.Of([TestEditor.BareStep()]) };
        using var session = TestEditor.Open(rule, clipboard);

        session.Select(Selection.At(new CellAddress(rule.Id)));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Paste));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("nothing here", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFragmentFromAnotherLanguageIsRefusedByName()
    {
        var (group, items) = GroupEditor.GroupWithItems("first");
        var clipboard = new InMemoryClipboard { Fragment = ModelFragment.Of(items) };
        var target = TestEditor.BareRule();
        using var session = TestEditor.Open(target, clipboard);

        TestEditor.SelectWhole(session, target);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Paste));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains(GroupLanguage.Item, result.Reason, StringComparison.Ordinal);
        Assert.Null(target.GetChild("body"));

        _ = group;
    }

    [Fact]
    public void AReferenceBetweenCopiedNodesPointsAtTheCopy()
    {
        var outer = TestEditor.BareRule();
        var inner = outer.Model.NewChild(outer, "extends", TestLanguage.Rule);
        inner.SetReference("inherits", outer);

        var target = TestEditor.BareRule();
        var clipboard = new InMemoryClipboard { Fragment = ModelFragment.Of([outer]) };
        using var session = TestEditor.Open(target, clipboard);

        session.Select(Selection.At(CellAddress.OfSlot(target.Id, "extends")));

        var result = session.Apply(KeyRouter.For(session, EditorKey.Paste));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var pasted = target.GetChild("extends")!;
        var pastedInner = pasted.GetChild("extends")!;

        Assert.NotEqual(outer.Id, pasted.Id);
        Assert.Equal(pasted.Id, pastedInner.GetReferenceTarget("inherits")?.Id);
        Assert.Equal(pasted.Id, pastedInner.GetReference("inherits")!.Value.Target);
    }

    [Fact]
    public void AReferenceOutOfTheFragmentKeepsBothHalvesAndDegradesToUnresolved()
    {
        var rule = TestEditor.BareRule();
        var tag = rule.Model.NewNode(TestLanguage.Tag);
        tag.Set("label", "finance");
        rule.Model.AddRoot(tag);
        rule.SetReference("tag", tag);

        // A second project: the tag's id means nothing here, which is what a paste into another document does.
        var target = TestEditor.BareRule();
        var clipboard = new InMemoryClipboard { Fragment = ModelFragment.Of([rule]) };
        using var session = TestEditor.Open(target, clipboard);

        session.Select(Selection.At(CellAddress.OfSlot(target.Id, "extends")));
        session.Apply(KeyRouter.For(session, EditorKey.Paste));

        var pasted = target.GetChild("extends")!;

        Assert.Equal(tag.Id, pasted.GetReference("tag")!.Value.Target);
        Assert.Equal("finance", pasted.GetReference("tag")!.Value.ResolveInfo);
        Assert.Null(pasted.GetReferenceTarget("tag"));
    }

    [Fact]
    public void APasteIsOneUndoStep()
    {
        var (source, copied) = TestEditor.BodyWithSteps("first", "second");
        var (target, _) = TestEditor.BodyWithSteps();
        var clipboard = new InMemoryClipboard { Fragment = ModelFragment.Of(copied) };
        using var session = TestEditor.Open(target, clipboard);

        session.Select(Selection.At(CellAddress.OfSlot(target.Id, "steps")));
        session.Apply(KeyRouter.For(session, EditorKey.Paste));

        Assert.Equal(2, target.GetChildren("steps").Count);

        Assert.True(session.Undo());
        Assert.Empty(target.GetChildren("steps"));
        Assert.False(session.History.CanUndo);

        _ = source;
    }

    [Fact]
    public void AFragmentThatHoldsNothingIsNothingToDo()
    {
        var step = TestEditor.BareStep("walk");
        var clipboard = new InMemoryClipboard { Fragment = new ModelFragment([]) };
        using var session = TestEditor.Open(step, clipboard);

        var result = session.Apply(new PasteFragmentAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.False(session.History.CanUndo);
    }
}
