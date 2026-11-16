using Avalonia.Input;
using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The offers gesture: two spellings of one key, two answers from the session, and the rules that close an
/// offer again.
/// </summary>
/// <remarks>
/// The answers are the whole design: nothing to offer, or a list - one entry or several, because the gesture
/// never applies an offer by itself. The closing rules matter as much, because a list that outlives the caret
/// it described offers actions for a place the user has left.
/// </remarks>
public sealed class OfferKeysTests
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
    public void TheGestureRoutesToTheOfferAction()
    {
        Assert.IsType<OfferAction>(KeyRouter.For(EditorKey.QuickFix));
        Assert.IsType<OfferAction>(KeyRouter.For(TestEditor.Open(TestEditor.BareRule()), EditorKey.QuickFix));
    }

    [Fact]
    public void OneRepairOpensAListToo()
    {
        // The gesture never applies anything by itself, not even the single cure for the single finding under
        // the caret: one keystroke must not sometimes act and sometimes ask.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        Publish(session, rule, "walked");

        var result = session.Apply(new OfferAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);
        Assert.Equal(1, Assert.IsType<OfferList>(session.Offered).Count);

        Assert.Equal(EditOutcome.Applied, session.Apply(new AcceptOfferAction()).Outcome);
        Assert.Equal("walked", rule.GetOrNull("name"));
        Assert.Null(session.Offered);

        // Accepting it is one step of history, like any other edit.
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

        var result = session.Apply(new OfferAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
        Assert.False(session.History.CanUndo);

        var offered = Assert.IsType<OfferList>(session.Offered);
        Assert.Equal(2, offered.Count);
        Assert.Equal(["Rename to \"first\"", "Rename to \"second\""], offered.Entries.Select(entry => entry.Label));

        // The list keeps the finding each repair belongs to, so a popup can show the problem next to it.
        Assert.Equal(second.Target, offered[1].Fix!.Diagnostic.Target);
    }

    [Fact]
    public void DownMovesTheHighlightAndNotTheCaret()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        var caret = session.Selection.Caret;

        session.Apply(new SelectOfferAction(1));

        Assert.Equal(1, session.Offered!.Selected);
        Assert.Equal(caret, session.Selection.Caret);

        // Clamped, not wrapped: a list of repairs is not a carousel.
        session.Apply(new SelectOfferAction(5));
        Assert.Equal(1, session.Offered!.Selected);

        session.Apply(new SelectOfferAction(-5));
        Assert.Equal(0, session.Offered!.Selected);
    }

    [Fact]
    public void EnterAppliesTheHighlightedRepairAndClosesTheList()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Apply(new SelectOfferAction(1));
        var result = session.Apply(new AcceptOfferAction());

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("second", rule.GetOrNull("name"));
        Assert.Null(session.Offered);
    }

    [Fact]
    public void AcceptingWithoutAListDoesNothing()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new AcceptOfferAction()).Outcome);
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

        Assert.IsType<SelectOfferAction>(KeyRouter.For(session, EditorKey.Down));
        Assert.IsType<SelectOfferAction>(KeyRouter.For(session, EditorKey.Up));
        Assert.IsType<AcceptOfferAction>(KeyRouter.For(session, EditorKey.Enter));
        Assert.IsType<MoveCaretAction>(KeyRouter.For(session, EditorKey.Left));
    }

    [Fact]
    public void NothingToOfferLeavesNoList()
    {
        using var session = TestEditor.Open(RuleNamed("walk"));

        var result = session.Apply(new OfferAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Null(session.Offered);
    }

    [Fact]
    public void AnotherActionClosesTheList()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Apply(new MoveCaretAction(CaretMove.Right));

        Assert.Null(session.Offered);
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

        Assert.Null(session.Offered);
    }

    [Fact]
    public void EscapeClosesTheList()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        OpenListOfTwo(session, rule, name);

        session.Apply(new DismissCompletionAction());

        Assert.Null(session.Offered);
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

        Assert.Null(session.Offered);
    }

    [Fact]
    public void ALoneDestructiveRepairIsListedLikeEverythingElse()
    {
        // It used to need a list of its own, because a lone plain offer ran. Now every offer gets a list;
        // what is still worth pinning is that the row is there and says what it would do.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        var finding = Finding(rule, "the name says nothing");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(DestructiveRenameFix(finding, name, "walked"));

        var result = session.Apply(new OfferAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);

        var offered = Assert.IsType<OfferList>(session.Offered);
        Assert.Equal(1, offered.Count);
    }

    [Fact]
    public void ALoneDestructiveRepairStillRunsFromTheListItOpened()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");
        var finding = Finding(rule, "the name says nothing");
        var fix = DestructiveRenameFix(finding, name, "walked");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);
        Assert.Equal(EditOutcome.Applied, session.Apply(new AcceptOfferAction()).Outcome);
        Assert.Equal("walked", rule.GetOrNull("name"));
    }

    [Fact]
    public void ALoneRepairThatCannotRunIsShownWithItsReason()
    {
        // Applying it silently would answer the user with nothing at all; the list at least says what is
        // there, and accepting the entry reports the reason the check gave.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var finding = Finding(rule, "the name says nothing");
        var fix = new DiagnosticFix(
            finding,
            "rename",
            "Rename to \"walked\"",
            _ => new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "walked"),
            availability: FixAvailability.Disabled("the document is open read-only"));

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);

        var offered = Assert.IsType<OfferList>(session.Offered);
        Assert.Equal(1, offered.Count);
        Assert.True(offered[0].IsDisabled);
    }

    [Fact]
    public void AListMayHoldOneEntry()
    {
        // One entry is the ordinary case now, not an exception for destructive or disabled offers.
        var rule = RuleNamed("walk");
        var name = CellAddress.OfSlot(rule.Id, "name");
        var finding = Finding(rule, "one");

        Assert.Throws<ArgumentException>(() => new OfferList([]));
        Assert.Throws<ArgumentNullException>(() => new OfferList(null!));

        var single = new OfferList([Intention.FromFix(DestructiveRenameFix(finding, name, "one"))]);

        Assert.Equal(1, single.Count);
        Assert.Equal(0, single.Selected);
        Assert.Equal("Rename to \"one\"", single.SelectedEntry.Label);
        Assert.True(single.SelectedEntry.IsDestructive);
    }

    [Fact]
    public void TheActionRefusesToRunOutsideASession()
    {
        Assert.Throws<EditingException>(() => new OfferAction().Apply(null!));
    }

    [Fact]
    public void OneIntentionOpensAListOfOne()
    {
        // An intention is a list entry like any other: the gesture shows what can be done and applies nothing.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        session.AddIntentions(new StubIntentions(IntentionNamed("test.rename", rule)));

        var result = session.Apply(new OfferAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal("walk", rule.GetOrNull("name"));
        Assert.Equal(1, session.Offered!.Count);

        Assert.Equal(EditOutcome.Applied, session.Apply(new AcceptOfferAction()).Outcome);
        Assert.Equal("renamed", rule.GetOrNull("name"));
        Assert.Null(session.Offered);
    }

    [Fact]
    public void ALoneDestructiveIntentionOpensAList()
    {
        // The safety rule is on the offer, so it covers both catalogues: the keystroke that asks what is
        // available must not be the one that deletes a node.
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        session.AddIntentions(new StubIntentions(new Intention(
            "test.delete",
            "Delete",
            "Removes the rule.",
            _ => new DeleteNodeAction(),
            safety: EditSafety.Destructive)));

        var result = session.Apply(new OfferAction());

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);

        var offered = Assert.IsType<OfferList>(session.Offered);

        Assert.Single(offered.Entries);
        Assert.Single(rule.Model.Roots);
    }

    [Fact]
    public void ALoneDisabledIntentionOpensAListSoItsReasonIsVisible()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        session.AddIntentions(new StubIntentions(new Intention(
            "test.blocked",
            "Blocked",
            "Cannot run here.",
            _ => null,
            availability: IntentionAvailability.Disabled("the document is read-only"))));

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);

        var offered = Assert.IsType<OfferList>(session.Offered);

        Assert.Equal(1, offered.Count);
        Assert.True(offered.SelectedEntry.IsDisabled);
        Assert.Equal("the document is read-only", offered.SelectedEntry.Availability.Reason);
    }

    [Fact]
    public void TheListHoldsIntentionsAndRepairsInOneOrder()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        var finding = Finding(rule, "the name says nothing");

        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);
        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(RenameFix(finding, CellAddress.OfSlot(rule.Id, "name"), "walked"));

        session.Apply(new OfferAction());

        var offered = Assert.IsType<OfferList>(session.Offered);

        Assert.Equal(
            [TestIntentions.RenameId, TestIntentions.DeleteId, "rename.walked"],
            offered.Entries.Select(entry => entry.Id));

        // The repair kept its finding, which is what lets accepting it go through the fix path.
        Assert.NotNull(offered[2].Fix);
        Assert.Null(offered[0].Fix);
    }

    [Fact]
    public void AcceptingAnIntentionRowAppliesTheIntention()
    {
        var rule = RuleNamed("walk");
        using var session = TestEditor.Open(rule);
        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);

        session.Apply(new OfferAction());
        session.Apply(new SelectOfferAction(0));
        var result = session.Apply(new AcceptOfferAction());

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("renamed", rule.GetOrNull("name"));
        Assert.Null(session.Offered);
    }

    [Fact]
    public void AnIntentionOfferedOnlyAwayFromTheCaretDoesNotLightTheGesture()
    {
        // The fixture answers "absent" off a rule, so a caret on a step has nothing to offer - and the
        // gesture then leaves no list behind.
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);
        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);
        Assert.Null(session.Offered);
    }

    private static Intention IntentionNamed(string id, Node rule) =>
        new(
            id,
            "Rename",
            "Renames the rule.",
            _ => new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "renamed"));

    private static void OpenListOfTwo(EditorSession session, Node rule, CellAddress name)
    {
        var first = Finding(rule, "one");
        var second = Finding(rule, "two");

        session.Markers = new MarkerSet(first, second);
        session.Fixes = new FixSet(RenameFix(first, name, "first"), RenameFix(second, name, "second"));

        session.Apply(new OfferAction());

        Assert.NotNull(session.Offered);
    }

    private static void Publish(EditorSession session, Node rule, string name)
    {
        var finding = Finding(rule, "the name says nothing");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(RenameFix(finding, CellAddress.OfSlot(rule.Id, "name"), name));
    }

    private static DiagnosticFix RenameFix(Diagnostic finding, CellAddress name, string text) =>
        new(finding, $"rename.{text}", $"Rename to \"{text}\"", _ => new SetPropertyAction(name, text));

    /// <summary>One repair that removes something, which is what makes the gesture ask first.</summary>
    private static DiagnosticFix DestructiveRenameFix(Diagnostic finding, CellAddress name, string text) =>
        new(
            finding,
            $"rename.{text}",
            $"Rename to \"{text}\"",
            _ => new SetPropertyAction(name, text),
            safety: EditSafety.Destructive);

    private static Diagnostic Finding(Node rule, string message) =>
        new(DiagnosticSeverity.Warning, rule.Id, message, role: "name", range: CharacterRange.Of(0, 4));

    private static Node RuleNamed(string name)
    {
        var rule = TestEditor.BareRule();
        rule.Set("name", name);

        return rule;
    }
}
