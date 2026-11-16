using Avalonia.Input;
using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The quick-fix gesture: two spellings of one key, three answers from the session, and the rules that
/// close an offer again.
/// </summary>
/// <remarks>
/// The three answers are the whole design: nothing to offer, one repair applied without asking, several
/// repairs listed. The closing rules matter as much, because a list that outlives the caret it described
/// offers repairs for a place the user has left.
/// </remarks>
public sealed class QuickFixKeysTests
{
    [Fact]
    public void AltEnterAndControlPeriodAreTheSameGesture()
    {
        Assert.Equal(EditorKey.QuickFix, EditorKeyMap.Map(Key.Enter, KeyModifiers.Alt));
        Assert.Equal(EditorKey.QuickFix, EditorKeyMap.Map(Key.OemPeriod, KeyModifiers.Control));
    }

    [Fact]
    public void PlainEnterIsStillAnInsertion()
    {
        // The gesture is the combination, not the key: taking Enter on its own would replace the key the
        // whole editor is written in terms of.
        Assert.Equal(EditorKey.Enter, EditorKeyMap.Map(Key.Enter, KeyModifiers.None));
    }

    [Fact]
    public void AnotherAltCombinationStaysWithTheHost()
    {
        Assert.Null(EditorKeyMap.Map(Key.Left, KeyModifiers.Alt));
        Assert.Null(EditorKeyMap.Map(Key.OemPeriod, KeyModifiers.None));
    }

    [Fact]
    public void TheGestureRoutesToTheQuickFixAction()
    {
        Assert.IsType<QuickFixAction>(KeyRouter.For(EditorKey.QuickFix));
        Assert.IsType<QuickFixAction>(KeyRouter.For(TestEditor.Open(TestEditor.BareRule()), EditorKey.QuickFix));
    }

    [Fact]
    public void OneRepairIsAppliedWithoutAsking()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        Publish(session, rule, "walked");

        var result = session.Apply(new QuickFixAction());

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("walked", rule.GetOrNull("name"));
        Assert.Null(session.OfferedFixes);

        // The gesture is one step of history, like any other edit.
        Assert.True(session.Undo());
        Assert.Equal("walk", rule.GetOrNull("name"));
    }

    [Fact]
    public void SeveralRepairsOpenAListAndChangeNothing()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        var first = Finding(rule, "one");
        var second = Finding(rule, "two");

        session.Markers = new MarkerSet(first, second);
        session.Fixes = new FixSet(RenameFix(first, name, "first"), RenameFix(second, name, "second"));

        var result = session.Apply(new QuickFixAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);

        var offered = Assert.IsType<QuickFixList>(session.OfferedFixes);
        Assert.Equal(2, offered.Count);
        Assert.Equal(["Rename to \"first\"", "Rename to \"second\""], offered.Fixes.Select(fix => fix.Label));

        // The list keeps the finding each repair belongs to, so a popup can show the problem next to it.
        Assert.Equal(second.Target, offered.DiagnosticOf(1).Target);
    }

    [Fact]
    public void DownMovesTheHighlightAndNotTheCaret()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        var caret = session.Selection.Caret;

        session.Apply(new SelectQuickFixAction(1));

        Assert.Equal(1, session.OfferedFixes!.Selected);
        Assert.Equal(caret, session.Selection.Caret);

        // Clamped, not wrapped: a list of repairs is not a carousel.
        session.Apply(new SelectQuickFixAction(5));
        Assert.Equal(1, session.OfferedFixes!.Selected);

        session.Apply(new SelectQuickFixAction(-5));
        Assert.Equal(0, session.OfferedFixes!.Selected);
    }

    [Fact]
    public void EnterAppliesTheHighlightedRepairAndClosesTheList()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Apply(new SelectQuickFixAction(1));
        var result = session.Apply(new AcceptQuickFixAction());

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("second", rule.GetOrNull("name"));
        Assert.Null(session.OfferedFixes);
    }

    [Fact]
    public void AcceptingWithoutAListDoesNothing()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new AcceptQuickFixAction()).Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
    }

    [Fact]
    public void TheKeysReachTheListWhileItIsOpen()
    {
        // The router has to know about the open list, or Down would move the caret out from under it.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        Assert.IsType<SelectQuickFixAction>(KeyRouter.For(session, EditorKey.Down));
        Assert.IsType<SelectQuickFixAction>(KeyRouter.For(session, EditorKey.Up));
        Assert.IsType<AcceptQuickFixAction>(KeyRouter.For(session, EditorKey.Enter));
        Assert.IsType<MoveCaretAction>(KeyRouter.For(session, EditorKey.Left));
    }

    [Fact]
    public void NothingToOfferLeavesNoList()
    {
        using var session = TestEditor.Open(RuleNamed("walk"));

        var result = session.Apply(new QuickFixAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Null(session.OfferedFixes);
    }

    [Fact]
    public void AnotherActionClosesTheList()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Apply(new MoveCaretAction(CaretMove.Right));

        Assert.Null(session.OfferedFixes);
    }

    [Fact]
    public void ACaretThatMovedClosesTheList()
    {
        // The list describes the findings of one line, so a caret on another line must not keep it up:
        // the box is drawn beside the caret, and would otherwise show this line's repairs over there.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Select(Selection.At(CaretPosition.At(name, 0)));

        Assert.Null(session.OfferedFixes);
    }

    [Fact]
    public void EscapeClosesTheList()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Apply(new DismissCompletionAction());

        Assert.Null(session.OfferedFixes);
    }

    [Fact]
    public void AModelChangeClosesTheList()
    {
        // The offer named a caret line of a layout the edit has just replaced.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Apply(new SetPropertyAction(name, "running"));

        Assert.Null(session.OfferedFixes);
    }

    [Fact]
    public void TheMustBeAtLeastTwoRepairsInAList()
    {
        var rule = RuleNamed("walk");
        var name = CellAddress.OfSlot(rule.Id, "name");
        var finding = Finding(rule, "one");

        Assert.Throws<ArgumentException>(() => new QuickFixList([]));
        Assert.Throws<ArgumentException>(() => new QuickFixList([RenameFix(finding, name, "one")]));
        Assert.Throws<ArgumentNullException>(() => new QuickFixList(null!));
    }

    [Fact]
    public void TheActionRefusesToRunOutsideASession()
    {
        Assert.Throws<EditingException>(() => new QuickFixAction().Apply(null!));
    }

    private static void OpenListOfTwo(EditorSession session, Node rule, CellAddress name)
    {
        var first = Finding(rule, "one");
        var second = Finding(rule, "two");

        session.Markers = new MarkerSet(first, second);
        session.Fixes = new FixSet(RenameFix(first, name, "first"), RenameFix(second, name, "second"));

        session.Apply(new QuickFixAction());

        Assert.NotNull(session.OfferedFixes);
    }

    private static void Publish(EditorSession session, Node rule, string name)
    {
        var finding = Finding(rule, "the name says nothing");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(RenameFix(finding, CellAddress.OfSlot(rule.Id, "name"), name));
    }

    private static DiagnosticFix RenameFix(Diagnostic finding, CellAddress name, string text) =>
        new(finding, $"rename.{text}", $"Rename to \"{text}\"", _ => new SetPropertyAction(name, text));

    private static Diagnostic Finding(Node rule, string message) =>
        new(DiagnosticSeverity.Warning, rule.Id, message, role: "name", range: CharacterRange.Of(0, 4));

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }
}
