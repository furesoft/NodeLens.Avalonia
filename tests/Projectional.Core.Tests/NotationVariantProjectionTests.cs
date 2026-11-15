using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A variant is chosen while projecting, so switching one is a re-projection and nothing else: the
/// model keeps its nodes, the cells keep their addresses, and only the text changes.
/// </summary>
public sealed class NotationVariantProjectionTests
{
    [Fact]
    public void WithNoChoiceANoteIsDrawnAsTheDefaultVariantSays() =>
        Assert.Equal(
            "note a, note b",
            LayoutSnapshot.Render(Body(), VariantNotations.Registry, width: 20));

    [Fact]
    public void TheChosenVariantDecidesWhatTheCellsAre() =>
        Assert.Equal(
            "(a), (b)",
            LayoutSnapshot.Render(
                Body(),
                VariantNotations.Registry,
                width: 20,
                choice: NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed)));

    [Fact]
    public void AChoiceAboutOneConceptLeavesEveryOtherConceptAsItWas() =>
        Assert.Equal(
            "note a, note b",
            LayoutSnapshot.Render(
                Body(),
                VariantNotations.Registry,
                width: 20,
                choice: NotationChoice.Default.With(TestLanguage.Body, VariantNotations.Unknown)));

    [Fact]
    public void AVariantThatHidesASlotStillDrawsTheNode()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);
        notations.Add(TestLanguage.Note, ed => ed.Line(ed.Keyword("note"), ed.Space, ed.Property("text")));
        notations.Add(TestLanguage.Note, "bare", ed => ed.Line(ed.Keyword("note")));
        notations.Add(TestLanguage.Body, ed => ed.Collection("notes", separator: ed.Punct(",")));

        Assert.Equal(
            "note, note",
            LayoutSnapshot.Render(Body(), notations, width: 20, choice: NotationChoice.Default.With(TestLanguage.Note, "bare")));
    }

    [Fact]
    public void SwitchingTheVariantReprojectsTheOpenDocument()
    {
        using var session = OpenBody();

        Assert.Equal("note a, note b", LayoutSnapshot.Render(session.Layout, width: 20));

        session.Choose(NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed));

        Assert.Equal("(a), (b)", LayoutSnapshot.Render(session.Layout, width: 20));
        Assert.Equal(VariantNotations.Bracketed, session.Choice.VariantOf(TestLanguage.Note));
    }

    [Fact]
    public void SwitchingBackDrawsTheDefaultVariantAgain()
    {
        using var session = OpenBody();
        session.Choose(NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed));

        session.Choose(null);

        Assert.Equal("note a, note b", LayoutSnapshot.Render(session.Layout, width: 20));
        Assert.Null(session.Choice.VariantOf(TestLanguage.Note));
    }

    [Fact]
    public void TheCaretStaysOnTheSlotItWasInWhenTheVariantChanges()
    {
        using var session = OpenBody();
        var before = session.Selection.Caret.Address;

        session.Choose(NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed));

        Assert.Equal(before, session.Selection.Caret.Address);
        Assert.Equal("text", session.Selection.Caret.Address.Slot);
    }

    [Fact]
    public void AChoiceNeverChangesTheModel()
    {
        using var session = OpenBody();
        var before = session.Document.ChildrenInDocumentOrder().Select(node => node.Id).ToArray();

        session.Choose(NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed));

        Assert.Equal(before, session.Document.ChildrenInDocumentOrder().Select(node => node.Id));
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void TwoSessionsOverOneDocumentCanDiffer()
    {
        var body = Body();
        using var plain = new EditorSession(body, VariantNotations.Registry);
        using var bracketed = new EditorSession(
            body,
            VariantNotations.Registry,
            choice: NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed));

        Assert.Equal("note a, note b", LayoutSnapshot.Render(plain.Layout, width: 20));
        Assert.Equal("(a), (b)", LayoutSnapshot.Render(bracketed.Layout, width: 20));
    }

    private static Node Body()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);
        model.NewChild(body, "notes", TestLanguage.Note).Set("text", "a");
        model.NewChild(body, "notes", TestLanguage.Note).Set("text", "b");

        return body;
    }

    private static EditorSession OpenBody() => new(Body(), VariantNotations.Registry);
}
