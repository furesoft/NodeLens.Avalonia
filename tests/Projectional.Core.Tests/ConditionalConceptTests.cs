using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A concept that names its places, seen from every direction it matters: the model refuses a wrong placement,
/// the completion list does not offer one, Enter does not create one, and a keystroke picks a concept that fits.
/// </summary>
public sealed class ConditionalConceptTests
{
    [Fact]
    public void TheModelRefusesAPlaceTheLanguageForbids()
    {
        // The guarantee. Everything the editor does about applicability is a courtesy on top of this, so a bug in
        // a completion filter can never produce a document the language does not allow.
        var model = ConditionalLanguage.Registry is var _ ? NewDocument() : null;
        var local = model.Roots[0].GetChildren(ConditionalLanguage.Members)[0];

        var refused = Assert.Throws<ModelInvariantException>(
            () => model.NewChild(local, ConditionalLanguage.Annotations, ConditionalLanguage.Annotation));

        Assert.Contains("may not stand in", refused.Message, StringComparison.Ordinal);
        Assert.Empty(local.GetChildren(ConditionalLanguage.Annotations));
    }

    [Fact]
    public void TheModelAcceptsAPlaceTheLanguageAllows()
    {
        var model = NewDocument();
        var document = model.Roots[0];

        var annotation = model.NewChild(document, ConditionalLanguage.Annotations, ConditionalLanguage.Annotation);

        Assert.Equal(ConditionalLanguage.Annotation, annotation.Concept.Key);
    }

    [Fact]
    public void AConceptThatNamesPlacesCannotBeARoot()
    {
        // A root has no parent and no slot, so none of its places can match - the same rule, not a special case.
        var model = NewDocument();

        Assert.Throws<ModelInvariantException>(
            () => model.AddRoot(model.NewNode(ConditionalLanguage.Annotation)));
    }

    [Fact]
    public void CompletionDoesNotOfferAConceptThatMayNotStandThere()
    {
        var (_, local) = OpenWithCaretIn(ConditionalLanguage.Local);

        var offered = CompletionSource.Proposals(ConditionalLanguage.Registry, local, ConditionalLanguage.Annotations);

        Assert.Equal([ConditionalLanguage.Comment], offered.Select(item => item.ConceptKey));
    }

    [Fact]
    public void CompletionOffersBothWhereBothAreAllowed()
    {
        // A class is both a place an annotation names and a member, so both concepts may stand there.
        var (_, @class) = OpenWithCaretIn(ConditionalLanguage.Class);

        var offered = CompletionSource.Proposals(ConditionalLanguage.Registry, @class, ConditionalLanguage.Annotations);

        Assert.Equal(
            [ConditionalLanguage.Annotation, ConditionalLanguage.Comment],
            offered.Select(item => item.ConceptKey));
    }

    [Fact]
    public void EnterSkipsAConceptThatMayNotStandThere()
    {
        // Annotation is declared first and is the language's default for the slot, but a local may not hold one,
        // so the next candidate answers instead of the model refusing what the editor created.
        var (session, local) = OpenWithCaretIn(ConditionalLanguage.Local);

        session.Select(Selection.At(CellAddress.OfSlot(local.Id, ConditionalLanguage.Annotations)));

        Assert.Equal(EditOutcome.Applied, session.Apply(new InsertChildAction(local.Id, ConditionalLanguage.Annotations)).Outcome);
        Assert.Equal(
            ConditionalLanguage.Comment,
            Assert.Single(local.GetChildren(ConditionalLanguage.Annotations)).Concept.Key);
    }

    [Fact]
    public void EnterTakesTheDeclaredDefaultWhereItFits()
    {
        var (session, document) = OpenWithCaretIn(ConditionalLanguage.Document);

        session.Select(Selection.At(CellAddress.OfSlot(document.Id, ConditionalLanguage.Annotations)));

        Assert.Equal(EditOutcome.Applied, session.Apply(new InsertChildAction(document.Id, ConditionalLanguage.Annotations)).Outcome);
        Assert.Equal(
            ConditionalLanguage.Annotation,
            Assert.Single(document.GetChildren(ConditionalLanguage.Annotations)).Concept.Key);
    }

    [Fact]
    public void TypingPicksAConceptThatMayStandThere()
    {
        var (session, local) = OpenWithCaretIn(ConditionalLanguage.Local);

        session.Select(Selection.At(CellAddress.OfSlot(local.Id, ConditionalLanguage.Annotations)));

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("@")).Outcome);
        Assert.Equal(
            ConditionalLanguage.Comment,
            Assert.Single(local.GetChildren(ConditionalLanguage.Annotations)).Concept.Key);
    }

    [Fact]
    public void TypingTakesTheFirstCandidateWhereItFits()
    {
        var (session, document) = OpenWithCaretIn(ConditionalLanguage.Document);

        session.Select(Selection.At(CellAddress.OfSlot(document.Id, ConditionalLanguage.Annotations)));

        session.Apply(KeyRouter.Type("@"));

        Assert.Equal(
            ConditionalLanguage.Annotation,
            Assert.Single(document.GetChildren(ConditionalLanguage.Annotations)).Concept.Key);
    }

    [Fact]
    public void APlaceOnASuperConceptFollowsInheritance()
    {
        // Comment names Member, so it is allowed in a class too - and a class is not a local, so the difference
        // between the two members is exactly what the place says.
        var model = NewDocument();
        var @class = model.NewChild(model.Roots[0], ConditionalLanguage.Members, ConditionalLanguage.Class);

        Assert.Equal(
            ConditionalLanguage.Comment,
            model.NewChild(@class, ConditionalLanguage.Annotations, ConditionalLanguage.Comment).Concept.Key);
    }

    /// <summary>A document with one local and one class member in it.</summary>
    private static Model NewDocument()
    {
        var model = new Project(ConditionalLanguage.Registry).NewModel("document");
        var document = model.NewNode(ConditionalLanguage.Document);

        model.AddRoot(document);
        model.NewChild(document, ConditionalLanguage.Members, ConditionalLanguage.Local);
        model.NewChild(document, ConditionalLanguage.Members, ConditionalLanguage.Class);

        return model;
    }

    /// <summary>A session over a fresh document, with <paramref name="ownerConceptKey"/> in the annotations slot.</summary>
    private static (EditorSession Session, Node Owner) OpenWithCaretIn(string ownerConceptKey)
    {
        var model = NewDocument();
        var document = model.Roots[0];

        var owner = ownerConceptKey == ConditionalLanguage.Document
            ? document
            : document.GetChildren(ConditionalLanguage.Members).First(member => member.Concept.Key == ownerConceptKey);

        var session = new EditorSession(
            document,
            ConditionalLanguage.Notations,
            typing: ConditionalLanguage.TypingRules);

        session.Select(Selection.At(CellAddress.OfSlot(owner.Id, ConditionalLanguage.Annotations)));

        return (session, owner);
    }
}
