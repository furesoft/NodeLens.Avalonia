using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Semantics;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The context menu as a description of what the caret allows: which entries appear, which of them can
/// be picked, and what picking one does.
/// </summary>
/// <remarks>
/// The menu is a decision rather than a widget, so the tests read the entries. The presenter and the
/// right-button handling in the control are the only parts that need a toolkit, and both are thin
/// enough to be judged by running the sample shell.
/// </remarks>
public sealed class EditorMenuTests
{
    private static NotationRegistry Notations => RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    [Fact]
    public void TheMenuOffersHistoryThenTheEdits()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        // The fixture nominates a collapse slot, so a node of it offers Fold. A language that does not opt
        // into folding has no such entry, which is what keeps this list language-dependent by design.
        Assert.Equal(
            ["Undo", "Redo", "", "Fold", "", "Insert", "Insert...", "Delete", "", "Select All", "Copy", "Cut", "Paste", "Go to Definition"],
            EditorMenu.For(session).Select(entry => entry.Label));
    }

    [Fact]
    public void AFoldableNodeOffersFoldAndThenUnfold()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var fold = Assert.IsType<EditorMenuItem>(
            EditorMenu.For(session).Single(entry => entry.Label is "Fold" or "Unfold"));

        Assert.Equal("Fold", fold.Label);
        Assert.False(fold.IsChecked);

        session.ToggleCollapsed(rule.Id);

        var unfold = Assert.IsType<EditorMenuItem>(
            EditorMenu.For(session).Single(entry => entry.Label is "Fold" or "Unfold"));

        Assert.Equal("Unfold", unfold.Label);
        Assert.True(unfold.IsChecked);
    }

    [Fact]
    public void ANodeThatCannotBeFoldedOffersNoEntry()
    {
        using var session = TestEditor.Open(TestEditor.BareStep());

        Assert.DoesNotContain(EditorMenu.For(session), entry => entry.Label is "Fold" or "Unfold");
    }

    [Fact]
    public void TheRepairsOfTheCaretLineComeFirstAsOneSubmenu()
    {
        // A line with repairs gets one submenu at the top rather than an entry per repair mixed into the
        // rest of the menu: the group is about where the caret is, and the entries under it are the same
        // ones the bulb and the gesture offer.
        var rule = TestEditor.BareRule();
        rule.Set("name", "walk");
        using var session = TestEditor.Open(rule);

        var finding = new Projectional.Semantics.Diagnostic(
            Projectional.Semantics.DiagnosticSeverity.Warning,
            rule.Id,
            "the name says nothing",
            role: "name");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(new DiagnosticFix(
            finding,
            "rename",
            "Rename to \"walked\"",
            _ => new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "walked"),
            "Gives the rule the name the finding suggests."));

        var submenu = Assert.IsType<EditorMenuItem>(EditorMenu.For(session)[0]);

        Assert.Equal("Quick fixes", submenu.Label);
        var entry = Assert.IsType<EditorMenuItem>(Assert.Single(submenu.Children));
        Assert.Equal("Rename to \"walked\"", entry.Label);
        Assert.Equal("Alt+Enter", entry.Gesture);
        Assert.Equal("Gives the rule the name the finding suggests.", entry.Description);

        entry.Execute();

        Assert.Equal("walked", rule.GetOrNull("name"));
    }

    [Fact]
    public void ALineWithNothingToRepairGetsNoSubmenuAtAll()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.DoesNotContain(EditorMenu.For(session), entry => entry.Label == "Quick fixes");
    }

    [Fact]
    public void AnIntentionGetsItsOwnSubmenu()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(new StubIntentions(new Intention(
            "test.rename",
            "Rename to \"walked\"",
            "Gives the rule the name the finding would suggest.",
            _ => new SetPropertyAction(CellAddress.OfSlot(rule.Id, "name"), "walked"),
            gesture: "Ctrl+T")));

        var submenu = Assert.IsType<EditorMenuItem>(Entry(session, "Intentions"));

        Assert.True(submenu.IsEnabled);

        var entry = Assert.IsType<EditorMenuItem>(Assert.Single(submenu.Children));

        Assert.Equal("Rename to \"walked\"", entry.Label);
        Assert.Equal("Ctrl+T", entry.Gesture);
        Assert.Equal("Gives the rule the name the finding would suggest.", entry.Description);
        Assert.True(entry.IsEnabled);

        entry.Execute();

        Assert.Equal("walked", rule.GetOrNull("name"));
    }

    [Fact]
    public void TheFixesComeBeforeTheIntentionsWhenTheCaretOffersBoth()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(new StubIntentions(new Intention(
            "test.rename",
            "Rename",
            "Renames the rule.",
            _ => null)));

        var finding = new Diagnostic(
            DiagnosticSeverity.Warning,
            rule.Id,
            "the name says nothing",
            role: "name");

        session.Markers = new MarkerSet(finding);
        session.Fixes = new FixSet(new DiagnosticFix(
            finding,
            "std.trim",
            "Trim",
            _ => null,
            "Removes the whitespace."));

        var labels = EditorMenu.For(session)
            .TakeWhile(entry => entry is EditorMenuItem)
            .Select(entry => entry.Label);

        Assert.Equal(["Quick fixes", "Intentions"], labels);
    }

    [Fact]
    public void ADisabledIntentionIsListedWithItsReason()
    {
        // The user can see that it will not run; the reason is the only part of the message that helps.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(new StubIntentions(new Intention(
            "test.blocked",
            "Blocked",
            "Cannot run here.",
            _ => null,
            availability: IntentionAvailability.Disabled("the document is read-only"))));

        var entry = Assert.IsType<EditorMenuItem>(
            Assert.Single(Assert.IsType<EditorMenuItem>(Entry(session, "Intentions")).Children));

        Assert.False(entry.IsEnabled);
        Assert.Equal("the document is read-only", entry.Description);
    }

    [Fact]
    public void AnIntentionThatIsAbsentHereGetsNoSubmenu()
    {
        // The fixture declares its entries once and answers "absent" off a rule, so the menu must not offer
        // a heading with nothing inside it.
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        session.AddIntentions(TestIntentions.ForRules, IntentionSource.Language);
        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));

        Assert.DoesNotContain(EditorMenu.For(session), entry => entry.Label == "Intentions");
    }

    [Fact]
    public void UndoIsAvailableOnceSomethingWasEditedAndRedoOnceItWasUndone()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step);

        Assert.False(Entry(session, "Undo").IsEnabled);
        Assert.False(Entry(session, "Redo").IsEnabled);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));
        session.Apply(KeyRouter.Type("a"));

        Assert.True(Entry(session, "Undo").IsEnabled);
        Assert.False(Entry(session, "Redo").IsEnabled);

        session.Undo();

        Assert.False(Entry(session, "Undo").IsEnabled);
        Assert.True(Entry(session, "Redo").IsEnabled);
    }

    [Fact]
    public void PickingUndoUndoesTheEdit()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(step.Id, "text"), 3)));
        session.Apply(KeyRouter.Type("d"));
        Assert.Equal("abcd", step.GetOrNull("text"));

        Entry(session, "Undo").Execute();

        Assert.Equal("abc", step.GetOrNull("text"));
    }

    [Fact]
    public void DeleteIsAvailableWhereThereIsACharacterToRemove()
    {
        var step = TestEditor.BareStep("ab");
        using var session = TestEditor.Open(step);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));

        Assert.True(Entry(session, "Delete").IsEnabled);
    }

    [Fact]
    public void DeleteIsUnavailableAtTheEndOfTheTextAndOutsideAProperty()
    {
        var step = TestEditor.BareStep("ab");
        using var session = TestEditor.Open(step);

        // Forward delete has nothing to the right of the last character.
        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(step.Id, "text"), 2)));
        Assert.False(Entry(session, "Delete").IsEnabled);

        session.Select(Selection.At(CaretNavigation.First(session.Tree)));
        Assert.False(Entry(session, "Delete").IsEnabled);
    }

    [Fact]
    public void ASelectionMakesDeleteAvailableEvenAtTheEndOfTheText()
    {
        var step = TestEditor.BareStep("ab");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 0), CaretPosition.At(text, 2)));

        Assert.True(Entry(session, "Delete").IsEnabled);
    }

    [Fact]
    public void PickingDeleteRemovesTheCharacterUnderTheCaret()
    {
        var step = TestEditor.BareStep("ab");
        using var session = TestEditor.Open(step);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));
        Entry(session, "Delete").Execute();

        Assert.Equal("b", step.GetOrNull("text"));
    }

    [Fact]
    public void AnEmptySlotNamesTheConceptEnterWouldCreate()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(body.Id, "steps")));

        // `Test.Step`, and the slot is declared over it - so the entry says what it will make rather than
        // leaving the user to press it and find out.
        var insert = Entry(session, "Insert \"Step\"");

        Assert.True(insert.IsEnabled);

        insert.Execute();

        var created = Assert.Single(body.GetChildren("steps"));

        Assert.Equal(CellAddress.OfSlot(created.Id, "index"), session.Selection.Caret.Address);
    }

    [Fact]
    public void InsertIsDisabledWhereEnterWouldDoNothing()
    {
        var step = TestEditor.BareStep("ab");
        using var session = TestEditor.Open(step);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(step.Id, "text"), 2)));

        var insert = Entry(session, "Insert");

        Assert.False(insert.IsEnabled);
        Assert.Equal("Enter adds a sibling only inside a list.", insert.Description);
    }

    [Fact]
    public void InsertEllipsisIsOfferedOnlyWhereCompletionWouldOpen()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        // A value: the list can always be asked for, even where it may come back empty.
        session.Select(Selection.At(CellAddress.OfSlot(rule.Id, "name")));
        Assert.True(Entry(session, "Insert...").IsEnabled);

        // A keyword of a root: there is no node here to replace and no slot to fill, so the entry would open
        // nothing at all.
        session.Select(Selection.At(new CellAddress(rule.Id)));
        Assert.False(Entry(session, "Insert...").IsEnabled);
    }

    [Fact]
    public void ADisabledEntryDoesNothing()
    {
        var step = TestEditor.BareStep("ab");
        using var session = TestEditor.Open(step);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(step.Id, "text"), 2)));

        Entry(session, "Delete").Execute();

        Assert.Equal("ab", step.GetOrNull("text"));
    }

    [Fact]
    public void EveryEntryButTheHistoryCarriesTheKeysThatDoTheSameThing()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Equal("Ctrl+Z", Entry(session, "Undo").Gesture);
        Assert.Equal("Ctrl+Y", Entry(session, "Redo").Gesture);
        Assert.Equal("Enter", Entry(session, "Insert").Gesture);
        Assert.Equal("Delete", Entry(session, "Delete").Gesture);
        Assert.Equal("Ctrl+A", Entry(session, "Select All").Gesture);
        Assert.Equal("Ctrl+C", Entry(session, "Copy").Gesture);
        Assert.Equal("Ctrl+X", Entry(session, "Cut").Gesture);
        Assert.Equal("Ctrl+V", Entry(session, "Paste").Gesture);
    }

    [Fact]
    public void CopyAndCutAreUnavailableWithoutATextSelection()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);

        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));

        Assert.False(Entry(session, "Copy").IsEnabled);
        Assert.False(Entry(session, "Cut").IsEnabled);
    }

    [Fact]
    public void CopyAndCutAreAvailableInsideAPropertyCell()
    {
        var step = TestEditor.BareStep("abc");
        using var session = TestEditor.Open(step);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 0), CaretPosition.At(text, 2)));

        Assert.True(Entry(session, "Copy").IsEnabled);
        Assert.True(Entry(session, "Cut").IsEnabled);
    }

    [Fact]
    public void CopyAndCutAreUnavailableWhereTheTextIsNotEditable()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var keyword = new CellAddress(rule.Id);

        // The `rule` keyword is a constant: its text can be selected, but copying it would promise a
        // clipboard the caret cannot type back into.
        session.Select(new Selection(CaretPosition.At(keyword, 0), CaretPosition.At(keyword, 4)));

        Assert.True(session.Selection.HasText);
        Assert.False(Entry(session, "Copy").IsEnabled);
        Assert.False(Entry(session, "Cut").IsEnabled);
    }

    [Fact]
    public void PasteIsUnavailableWhileTheClipboardIsEmpty()
    {
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(TestEditor.BareStep(), clipboard);

        Assert.False(Entry(session, "Paste").IsEnabled);

        clipboard.Text = "";
        Assert.False(Entry(session, "Paste").IsEnabled);

        clipboard.Text = "abc";
        Assert.True(Entry(session, "Paste").IsEnabled);
    }

    [Fact]
    public void PickingCopyLeavesTheSelectionOnTheClipboard()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));
        Entry(session, "Copy").Execute();

        Assert.Equal("bcd", clipboard.Text);
        Assert.Equal("abcdef", step.GetOrNull("text"));
    }

    [Fact]
    public void PickingCutRemovesTheSelectionAndLeavesItOnTheClipboard()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard();
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));
        Entry(session, "Cut").Execute();

        Assert.Equal("bcd", clipboard.Text);
        Assert.Equal("aef", step.GetOrNull("text"));
    }

    [Fact]
    public void PickingPasteTypesTheClipboardTextAtTheCaret()
    {
        var step = TestEditor.BareStep("abcdef");
        var clipboard = new InMemoryClipboard { Text = "XY" };
        using var session = TestEditor.Open(step, clipboard);
        var text = CellAddress.OfSlot(step.Id, "text");

        session.Select(new Selection(CaretPosition.At(text, 1), CaretPosition.At(text, 4)));
        Entry(session, "Paste").Execute();

        Assert.Equal("aXYef", step.GetOrNull("text"));
    }

    [Fact]
    public void NoRepresentationIsOfferedWhenTheDocumentHasNothingToChoose()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Empty(EditorMenu.Representations(session));
        Assert.DoesNotContain(EditorMenu.For(session), entry => entry.Label == "Representation");
    }

    [Fact]
    public void TheRepresentationSubmenuListsEveryVariantOfEveryConceptThatHasOne()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        var concept = Assert.IsType<EditorMenuItem>(Assert.Single(EditorMenu.Representations(session)));

        Assert.Equal(RulesConceptKeys.Ruleset, concept.Label);
        Assert.Equal(
            ["default", RulesLanguage.TableVariant],
            concept.Children.Select(entry => entry.Label));
        Assert.All(concept.Children, entry => Assert.True(Assert.IsType<EditorMenuItem>(entry).IsOption));
    }

    [Fact]
    public void TheVariantInForceIsMarkedAndCannotBePickedAgain()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        var concept = Assert.IsType<EditorMenuItem>(Assert.Single(EditorMenu.Representations(session)));
        var current = Assert.IsType<EditorMenuItem>(concept.Children[0]);
        var other = Assert.IsType<EditorMenuItem>(concept.Children[1]);

        Assert.Equal(Notation.DefaultVariant, current.Label);
        Assert.True(current.IsChecked);
        Assert.False(current.IsEnabled);

        Assert.False(other.IsChecked);
        Assert.True(other.IsEnabled);
    }

    [Fact]
    public void PickingAnotherVariantReProjectsTheDocument()
    {
        using var session = new EditorSession(PayrollSample.NewSampleModel().Roots[0], Notations);

        var concept = Assert.IsType<EditorMenuItem>(Assert.Single(EditorMenu.Representations(session)));
        Assert.IsType<EditorMenuItem>(concept.Children[1]).Execute();

        Assert.Equal(RulesLanguage.TableVariant, Assert.Single(session.Alternatives()).Selected);
        Assert.Contains("rule     then", LayoutSnapshot.Render(session.Layout, width: 60), StringComparison.Ordinal);
    }

    private static EditorMenuItem Entry(EditorSession session, string label) =>
        Assert.IsType<EditorMenuItem>(
            EditorMenu.For(session).Single(entry => string.Equals(entry.Label, label, StringComparison.Ordinal)));
}
