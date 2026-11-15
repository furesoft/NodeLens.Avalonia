using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What Ctrl+A means in a projectional editor: it widens one unit per press - the value, then the node,
/// then the document.
/// </summary>
/// <remarks>
/// A projectional document nests three units and "everything" is ambiguous between them, so the gesture
/// is a ladder rather than a single answer. The tests pin the rungs, the order, that a rung with nothing
/// to offer is skipped rather than blocking, and that the top rung does not move.
/// </remarks>
public sealed class SelectAllActionTests
{
    [Fact]
    public void SelectAllSelectsTheWholePropertyTheCaretStandsIn()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(text, session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.Equal(text, session.Selection.Caret.Address);
        Assert.Equal(3, session.Selection.Caret.Offset);
        Assert.True(session.Selection.HasText);
    }

    [Fact]
    public void SelectAllDoesNotChangeTheModelOrTheHistory()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.False(result.Changed);
        Assert.Equal(0, session.History.UndoDepth);
        Assert.Equal("abc", step.Get("text"));
    }

    [Fact]
    public void SelectAllWidensFromTheValueToTheNodeToTheDocument()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        step.Set("text", "abc");
        using var session = TestEditor.Open(rule);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 1)));

        session.Apply(KeyRouter.For(EditorKey.SelectAll));
        Assert.Equal(text, session.Selection.Anchor.Address);
        Assert.Equal(text, session.Selection.Caret.Address);
        Assert.True(session.Selection.HasText);

        session.Apply(KeyRouter.For(EditorKey.SelectAll));
        Assert.Equal(CellAddress.OfSlot(step.Id, "index"), session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.Equal(step.Id, session.Selection.Caret.Address.Owner);
        Assert.False(session.Selection.IsSingleCell);

        session.Apply(KeyRouter.For(EditorKey.SelectAll));
        Assert.Equal(new CellAddress(rule.Id), session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.Equal(new CellAddress(rule.Id), session.Selection.Caret.Address);
        Assert.Equal(1, session.Selection.Caret.Offset);
    }

    [Fact]
    public void SelectAllAtTheDocumentChangesNothing()
    {
        var (rule, _, _) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);
        var press = KeyRouter.For(EditorKey.SelectAll);

        session.Apply(press);
        session.Apply(press);
        session.Apply(press);

        var atDocument = session.Selection;
        var result = session.Apply(press);

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(atDocument.Anchor.Address, session.Selection.Anchor.Address);
        Assert.Equal(atDocument.Anchor.Offset, session.Selection.Anchor.Offset);
        Assert.Equal(atDocument.Caret.Address, session.Selection.Caret.Address);
        Assert.Equal(atDocument.Caret.Offset, session.Selection.Caret.Offset);
    }

    [Fact]
    public void SelectAllInAKeywordSelectsTheNode()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        // The node's own address resolves to the first caret place with it, which is the "rule" keyword.
        session.Select(Selection.At(new CellAddress(rule.Id)));

        var result = session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(new CellAddress(rule.Id), session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.False(session.Selection.IsEmpty);
    }

    [Fact]
    public void SelectAllInAnEmptyPropertySelectsTheNode()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(Selection.At(CaretPosition.At(text, 0)));

        session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(CellAddress.OfSlot(step.Id, "index"), session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.False(session.Selection.IsSingleCell);
    }

    [Fact]
    public void SelectAllInAnEmptySlotSelectsTheNodeThatOwnsIt()
    {
        var (_, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(body);

        // The body's steps slot has no children, so it projects as one empty collection cell.
        session.Select(Selection.At(CellAddress.OfSlot(body.Id, "steps")));

        session.Apply(KeyRouter.For(EditorKey.SelectAll));

        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), session.Selection.Anchor.Address);
        Assert.Equal(0, session.Selection.Anchor.Offset);
        Assert.Equal(CellAddress.OfSlot(body.Id, "notes"), session.Selection.Caret.Address);
        Assert.False(session.Selection.IsSingleCell);
    }

    [Fact]
    public void SelectAllRejectsANullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new SelectAllAction().Apply(null!));
    }
}