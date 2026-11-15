using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Enter is the one key whose meaning depends on where the caret stands: in a value of text it opens a
/// line, and everywhere else it keeps its structural meaning of inserting a node.
/// </summary>
/// <remarks>
/// The wrapping width is view state the host publishes, so it is set here on the session directly. What
/// a control adds is only the moment it learns the width - its measure pass - and nothing about which
/// line a value is broken into.
/// </remarks>
public sealed class MultiLineEditingTests
{
    [Fact]
    public void EnterInAValueOpensALineAtTheCaret()
    {
        var rule = RuleNamed("abcd");
        using var session = TestEditor.Open(rule);
        Name(session, rule, offset: 2);

        var result = session.Apply(KeyRouter.For(session, EditorKey.Enter));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("ab\ncd", rule.Get("name"));

        // The caret is on the line it just opened rather than at the end of the value.
        Assert.Equal(3, session.Selection.Caret.Offset);
    }

    [Fact]
    public void EnterAtTheEndOfAValueLeavesTheCaretOnAnEmptyLine()
    {
        var rule = RuleNamed("ab");
        using var session = TestEditor.Open(rule);
        Name(session, rule, offset: 2);

        session.Apply(KeyRouter.For(session, EditorKey.Enter));

        // A trailing break is a line of its own, which is what makes the key useful for typing on.
        Assert.Equal("ab\n", rule.Get("name"));
        Assert.Equal(2, session.Layout.LinesOf(NameCell(session, rule))!.LineCount);
        Assert.Equal(3, session.Selection.Caret.Offset);
    }

    [Fact]
    public void EnterReplacesASelectionWithABreak()
    {
        var rule = RuleNamed("abcd");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        session.Select(new Selection(CaretPosition.At(name, 1), CaretPosition.At(name, 3)));

        session.Apply(KeyRouter.For(session, EditorKey.Enter));

        Assert.Equal("a\nd", rule.Get("name"));
        Assert.Equal(2, session.Selection.Caret.Offset);
    }

    [Fact]
    public void TheRouterTellsTheTwoUsesOfEnterApart()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        Name(session, rule, offset: 0);
        Assert.IsType<InsertLineBreakAction>(KeyRouter.For(session, EditorKey.Enter));

        // An empty slot has no text of its own to break.
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "body")));
        Assert.IsType<InsertNodeAction>(KeyRouter.For(session, EditorKey.Enter));
    }

    [Fact]
    public void TheRouterStillDeletesTextWhileThereIsTextLeft()
    {
        var rule = RuleNamed("ab");
        using var session = TestEditor.Open(rule);

        Name(session, rule, offset: 1);

        // Enter and deletion are decided by the caret, but a cell with text left still deletes a character,
        // so the stateful answer is the stateless one.
        Assert.Equal(
            KeyRouter.For(EditorKey.Backspace).ToString(),
            KeyRouter.For(session, EditorKey.Backspace).ToString());
    }

    [Fact]
    public void EnterInsertsANodeWhereTheValueCannotHoldALine()
    {
        // A number is typed but not broken: a break there would be a rejected edit nobody asked for.
        var step = TestEditor.BareStep("walk");
        using var session = TestEditor.Open(step);
        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "index")));

        Assert.IsType<InsertNodeAction>(KeyRouter.For(session, EditorKey.Enter));
    }

    [Fact]
    public void EnterStillInsertsANodeInAnEmptySlot()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "body")));

        session.Apply(KeyRouter.For(session, EditorKey.Enter));

        Assert.Equal(TestLanguage.Body, Assert.Single(rule.GetChildren("body")).Concept.Key);
    }

    [Fact]
    public void ALineBreakIsOneUndoStep()
    {
        var rule = RuleNamed("ab");
        using var session = TestEditor.Open(rule);
        Name(session, rule, offset: 1);

        session.Apply(KeyRouter.For(session, EditorKey.Enter));

        Assert.Equal("a\nb", rule.Get("name"));
        Assert.True(session.Undo());
        Assert.Equal("ab", rule.Get("name"));
        Assert.True(session.Redo());
        Assert.Equal("a\nb", rule.Get("name"));
    }

    [Fact]
    public void TheSessionWrapsAValueAtTheWidthItIsGiven()
    {
        var rule = RuleNamed("abcdefghij");
        using var session = TestEditor.Open(rule);

        // No width is no page: the value runs on however long it is.
        Assert.Null(session.WrapWidth);
        Assert.True(session.Layout.LinesOf(NameCell(session, rule))!.IsSingleLine);

        // "rule " is five columns, so ten characters do not fit in a page of twelve.
        session.WrapWidth = 12;

        var lines = session.Layout.LinesOf(NameCell(session, rule));
        Assert.NotNull(lines);
        Assert.Equal(2, lines.LineCount);
        Assert.Equal(2, session.Layout.BoundsOf(NameCell(session, rule))!.Value.Height);
    }

    [Fact]
    public void ANewWidthKeepsTheCaretOnTheSameCharacter()
    {
        var rule = RuleNamed("abcdefghij");
        using var session = TestEditor.Open(rule);

        // One column into the continuation line once the value is wrapped.
        Name(session, rule, offset: 8);

        session.WrapWidth = 12;

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), session.Selection.Caret.Address);
        Assert.Equal(8, session.Selection.Caret.Offset);
    }

    [Fact]
    public void ClearingTheWidthReturnsTheValueToOneLine()
    {
        var rule = RuleNamed("abcdefghij");
        using var session = TestEditor.Open(rule);

        session.WrapWidth = 12;
        session.WrapWidth = null;

        Assert.True(session.Layout.LinesOf(NameCell(session, rule))!.IsSingleLine);
    }

    [Fact]
    public void SettingTheSameWidthAgainChangesNothing()
    {
        var rule = RuleNamed("abcdefghij");
        using var session = TestEditor.Open(rule);

        session.WrapWidth = 12;
        var laidOut = session.Layout;

        // The measure pass of a control publishes its width on every frame, so the setter has to be
        // free when nothing moved - a rebuild per frame would drop the layout the caret was measured in.
        session.WrapWidth = 12;

        Assert.Same(laidOut, session.Layout);
    }

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }

    private static void Name(EditorSession session, Node rule, int offset) =>
        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"), offset)));

    private static EditorCell NameCell(EditorSession session, Node rule) =>
        session.Tree.Root.Children[0].Children[2];
}
