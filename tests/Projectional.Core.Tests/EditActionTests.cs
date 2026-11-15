using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The actions are the whole of the editing vocabulary, so these tests are the contract a host can
/// rely on: what typing does inside a property, what a rejected value leaves behind, and where the
/// caret ends up afterwards.
/// </summary>
/// <remarks>
/// A step of <see cref="TestLanguage"/> projects as <c>index: text detail</c> with no constant text
/// of its own, which makes it the cheapest document to type into: its first slot is <c>index</c>, an
/// integer that starts out unset, and its <c>text</c> starts out unset as well. A step is also a leaf
/// list element, so it is where the tests go for insertion and deletion of text.
/// </remarks>
public sealed class EditActionTests
{
    [Fact]
    public void TypingWritesThePropertyAndMovesTheCaretBehindTheText()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);

        var result = session.Apply(KeyRouter.Type("7"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal(7, step.GetInt("index"));
        Assert.Equal(CellAddress.OfSlot(step.Id, "index"), session.Selection.Caret.Address);
        Assert.Equal(1, session.Selection.Caret.Offset);
        Assert.True(session.Selection.IsEmpty);
    }

    [Fact]
    public void TypingReplacesTheSelection()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 3)));
        session.Apply(KeyRouter.Type("X"));

        Assert.Equal("aX", step.GetOrNull("text"));
        Assert.Equal(2, session.Selection.Caret.Offset);
    }

    [Fact]
    public void TypingIntoAPropertyThatShowsItsDefaultInsertsInsteadOfReplacing()
    {
        // A new node materialises the defaults of its concept, so the name of a fresh rule is the
        // string "unnamed" rather than nothing. The model cannot tell a value the user typed from one
        // that was filled in for it, so typing inserts. Replacing the default needs a notion of an
        // unset property that renders a placeholder, which the model does not have yet.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Apply(KeyRouter.Type("P"));

        Assert.Equal("Punnamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void ConsecutiveKeystrokesInOneCellAreASingleUndoStep()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        session.Select(Selection.At(name));
        session.Apply(KeyRouter.Type("1"));
        session.Apply(KeyRouter.Type("2"));

        Assert.Equal("12unnamed", rule.GetOrNull("name"));
        Assert.Equal(1, session.History.UndoDepth);

        Assert.True(session.Undo());
        Assert.Equal("unnamed", rule.GetOrNull("name"));

        Assert.True(session.Redo());
        Assert.Equal("12unnamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void TypingAValueThePropertyRejectsLeavesItAlone()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);

        var result = session.Apply(KeyRouter.Type("x"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Null(step.GetOrNull("index"));
        Assert.False(result.Changed);
    }

    [Fact]
    public void TypingIntoAKeywordDoesNothing()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(new CellAddress(rule.Id)));
        var result = session.Apply(KeyRouter.Type("z"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("unnamed", rule.GetOrNull("name"));
    }

    [Fact]
    public void TypingIntoAReferenceDoesNothing()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "tag")));
        var result = session.Apply(KeyRouter.Type("z"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Null(rule.GetReference("tag"));
    }

    [Fact]
    public void TypingIntoAConceptWithoutANotationDoesNothing()
    {
        var tag = TestEditor.BareTag();
        using var session = TestEditor.Open(tag);

        var result = session.Apply(KeyRouter.Type("z"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.IsType<MissingNotationCell>(CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret)?.Cell);
    }

    [Fact]
    public void BackspaceAtTheStartOfACellDoesNothing()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(text));
        var result = session.Apply(KeyRouter.For(EditorKey.Backspace));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("abc", step.GetOrNull("text"));
    }

    [Fact]
    public void BackspaceRemovesTheCharacterBeforeTheCaret()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 2), CaretPosition.At(text, 2)));
        var result = session.Apply(KeyRouter.For(EditorKey.Backspace));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("ac", step.GetOrNull("text"));

        // The caret sits in the same cell, but the cell is a different one after the rebuild.
        Assert.Equal(text, session.Selection.Caret.Address);
        Assert.Equal(1, session.Selection.Caret.Offset);
    }

    [Fact]
    public void DeleteAtTheEndOfACellDoesNothing()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 3), CaretPosition.At(text, 3)));
        var result = session.Apply(KeyRouter.For(EditorKey.Delete));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("abc", step.GetOrNull("text"));
    }

    [Fact]
    public void DeleteRemovesTheCharacterBehindTheCaret()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 1)));
        session.Apply(KeyRouter.For(EditorKey.Delete));

        Assert.Equal("ac", step.GetOrNull("text"));
        Assert.Equal(1, session.Selection.Caret.Offset);
    }

    [Fact]
    public void DeleteRemovesASelection()
    {
        var step = TestEditor.BareStep("abcd");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 3)));
        session.Apply(KeyRouter.For(EditorKey.Backspace));

        Assert.Equal("ad", step.GetOrNull("text"));
        Assert.Equal(1, session.Selection.Caret.Offset);
    }

    [Fact]
    public void EnterFillsAnEmptySlotWithANewChild()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "body")));
        var result = session.Apply(KeyRouter.For(EditorKey.Enter));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var created = Assert.Single(rule.GetChildren("body"));
        Assert.Equal(TestLanguage.Body, created.Concept.Key);
        Assert.Empty(created.GetChildren("steps"));

        // The caret moves into the child that was added and lands on its first slot, which is where
        // the next keystroke has to go.
        Assert.Equal(CellAddress.OfSlot(created.Id, "steps"), session.Selection.Caret.Address);
    }

    [Fact]
    public void EnterInsideAListAppendsASibling()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        var first = body.Model.NewChild(body, "steps", TestLanguage.Step);
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(first.Id, "index")));
        session.Apply(KeyRouter.For(EditorKey.Enter));

        var steps = body.GetChildren("steps");
        Assert.Equal(2, steps.Count);
        Assert.Equal(1, steps[1].IndexInParent);
        Assert.Equal(CellAddress.OfSlot(steps[1].Id, "index"), session.Selection.Caret.Address);
    }

    [Fact]
    public void EnterOnANodeThatCannotTakeSiblingsDoesNothing()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));
        var result = session.Apply(KeyRouter.For(EditorKey.Enter));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Empty(rule.GetChildren("body"));
    }

    [Fact]
    public void TabWalksTheSlotsOfANodeAndStopsAtTheLastOne()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        Assert.Equal("name", session.Selection.Caret.Address.Slot);

        session.Apply(KeyRouter.For(EditorKey.Tab));
        Assert.Equal("body", session.Selection.Caret.Address.Slot);

        session.Apply(KeyRouter.For(EditorKey.Tab));
        Assert.Equal("extends", session.Selection.Caret.Address.Slot);
        Assert.Equal(0, session.Selection.Caret.Offset);

        var atLast = session.Apply(KeyRouter.For(EditorKey.Tab));
        Assert.Equal(EditOutcome.Unchanged, atLast.Outcome);
        Assert.Equal("extends", session.Selection.Caret.Address.Slot);
    }

    [Fact]
    public void ShiftTabWalksTheSlotsBackwardsAndStopsAtTheFirstOne()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.Apply(KeyRouter.For(EditorKey.Tab));
        session.Apply(KeyRouter.For(EditorKey.Tab));
        Assert.Equal("extends", session.Selection.Caret.Address.Slot);

        session.Apply(KeyRouter.For(EditorKey.ShiftTab));
        Assert.Equal("body", session.Selection.Caret.Address.Slot);

        session.Apply(KeyRouter.For(EditorKey.ShiftTab));
        Assert.Equal("name", session.Selection.Caret.Address.Slot);

        var atFirst = session.Apply(KeyRouter.For(EditorKey.ShiftTab));
        Assert.Equal(EditOutcome.Unchanged, atFirst.Outcome);
        Assert.Equal("name", session.Selection.Caret.Address.Slot);
    }

    [Fact]
    public void TabEntersAFilledBodyInsteadOfStoppingAtIt()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        // A body with content is not an empty slot any more, so the walk continues into the cells of
        // the child instead of offering the slot that is already taken.
        session.Apply(KeyRouter.For(EditorKey.Tab));
        Assert.Equal(CellAddress.OfSlot(step.Id, "index"), session.Selection.Caret.Address);

        session.Apply(KeyRouter.For(EditorKey.Tab));
        Assert.Equal(CellAddress.OfSlot(step.Id, "text"), session.Selection.Caret.Address);
    }

    [Fact]
    public void EnterInASlotDeclaredOverAnAbstractConceptCreatesTheLanguagesDefault()
    {
        var holder = AbstractSlotLanguage.NewHolder();
        using var session = AbstractSlotLanguage.Open(holder);

        session.Select(Selection.At(CellAddress.OfSlot(holder.Id, "value")));
        var result = session.Apply(KeyRouter.For(EditorKey.Enter));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var created = Assert.Single(holder.GetChildren("value"));
        Assert.Equal(AbstractSlotLanguage.Number, created.Concept.Key);

        // The caret still lands inside what was created, and the created node's own first slot is an
        // integer property, so the next keystroke types a number.
        Assert.Equal(CellAddress.OfSlot(created.Id, "number"), session.Selection.Caret.Address);
    }

    [Fact]
    public void EnterInASlotWithNoConcreteConceptDoesNotInventANode()
    {
        var holder = AbstractSlotLanguage.NewHolder();
        using var session = AbstractSlotLanguage.Open(holder);

        session.Select(Selection.At(CellAddress.OfSlot(holder.Id, "sealed")));
        var result = session.Apply(KeyRouter.For(EditorKey.Enter));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Empty(holder.GetChildren("sealed"));
    }

    [Fact]
    public void AnActionNeedsAContext()
    {
        Assert.Throws<ArgumentNullException>(() => new TypeTextAction("a").Apply(null!));
    }
}
