using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;

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

        Assert.Equal(
            ["Undo", "Redo", "", "Insert", "Delete", "", "Select All", "Copy", "Cut", "Paste"],
            EditorMenu.For(session).Select(entry => entry.Label));
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
    public void InsertIsAlwaysOfferedAndPickingItFillsTheEmptySlot()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CellAddress.OfSlot(body.Id, "steps")));

        var insert = Entry(session, "Insert");

        Assert.True(insert.IsEnabled);

        insert.Execute();

        var created = Assert.Single(body.GetChildren("steps"));

        Assert.Equal(CellAddress.OfSlot(created.Id, "index"), session.Selection.Caret.Address);
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
