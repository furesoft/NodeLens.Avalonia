using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Structures;
using Projectional.Structures.Intentions;
using Projectional.Structures.Notation;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Making a concept abstract and making it concrete again, which is the operation the MPS-shaped notation needs
/// because it draws the flag as a word instead of as a value.
/// </summary>
/// <remarks>
/// The list is asked through <see cref="IntentionList.For"/> rather than through the provider, because the point of
/// the feature is where the entry ends up: in the list the user sees, one line long, in the language group.
/// </remarks>
public sealed class StructureIntentionsTests
{
    [Fact]
    public void AConcreteConceptOffersToBecomeAbstract()
    {
        var (session, _) = Document(isAbstract: false);

        var offer = ById(session, StructureIntentions.MakeAbstractId);

        Assert.Equal("Make abstract", offer.Label);
        Assert.False(string.IsNullOrWhiteSpace(offer.Description));
        Assert.True(offer.Availability.IsApplicable);
        Assert.Equal(EditSafety.Plain, offer.Safety);
    }

    [Fact]
    public void AnAbstractConceptOffersToBecomeConcrete()
    {
        var (session, _) = Document(isAbstract: true);

        // "Make abstract" is absent rather than disabled - an entry that would change nothing is noise, and the
        // catalogue drops it - so what is left at an abstract concept is the direction that changes something,
        // followed by the two entries that can still add a first inheritance, because this fixture has neither.
        Assert.Equal(
            [
                StructureIntentions.MakeConcreteId,
                StructureIntentions.MakeInterfaceId,
                StructureIntentions.InheritFromId,
                StructureIntentions.ImplementInterfaceId,
            ],
            IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void AnInterfaceOffersNeitherDirectionAndOffersToStopBeingOne()
    {
        // An interface is abstract because of what it is, so neither "make concrete" (which the registry would
        // refuse) nor "make abstract" (which it already means) belongs in the list.
        var (session, _) = Document(isAbstract: true, isInterface: true);

        Assert.Equal(
            [
                StructureIntentions.MakeConceptId,
                StructureIntentions.InheritFromId,
                StructureIntentions.ImplementInterfaceId,
            ],
            IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void ADocumentIsRewrittenInOneUndoStep()
    {
        var (session, concept) = Document(isAbstract: false);

        // Before: a concrete concept says only `concept`.
        Assert.Contains("concept Rules.Rule", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
        Assert.DoesNotContain("abstract", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);

        var result = session.Apply(ById(session, StructureIntentions.MakeAbstractId));

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("true", concept.GetOrNull(StructureLanguage.IsAbstractProperty));

        // After: the word is part of the header, which is the whole reason the value has no cell.
        var drawn = LayoutSnapshot.Render(session.Layout, width: 120);

        Assert.Contains("abstract concept Rules.Rule", drawn, StringComparison.Ordinal);

        Assert.True(session.Undo());
        Assert.Equal("false", concept.GetOrNull(StructureLanguage.IsAbstractProperty));
        Assert.DoesNotContain("abstract", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void MakingAConceptAnInterfaceSetsBothFlagsAndTheHeaderSaysInterface()
    {
        var (session, concept) = Document(isAbstract: false);

        Assert.Equal(EditOutcome.Applied, session.Apply(ById(session, StructureIntentions.MakeInterfaceId)).Outcome);

        // Both, in one undo step: an interface that is not abstract is the combination the registry refuses.
        Assert.Equal("true", concept.GetOrNull(StructureLanguage.IsInterfaceProperty));
        Assert.Equal("true", concept.GetOrNull(StructureLanguage.IsAbstractProperty));
        Assert.Contains("interface concept Rules.Rule", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);

        Assert.True(session.Undo());
        Assert.Equal("false", concept.GetOrNull(StructureLanguage.IsInterfaceProperty));
        Assert.Equal("false", concept.GetOrNull(StructureLanguage.IsAbstractProperty));
    }

    [Fact]
    public void AConceptWithAChildSlotCannotBecomeAnInterface()
    {
        // The registry refuses an interface with a child slot, so the entry declines instead of producing a
        // definition the language would then reject.
        var definition = new DefinitionBuilder();
        var concept = definition.Concept(
            "Rules.Rule",
            slots: [definition.Child("body", "Rules.Body")]);

        using var session = new EditorSession(concept, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(new CellAddress(concept.Id)));

        var offer = ById(session, StructureIntentions.MakeInterfaceId);

        Assert.False(offer.Availability.IsApplicable);
        Assert.Contains("child slot", offer.Availability.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOtherDirectionWorksTheSameWay()
    {
        var (session, concept) = Document(isAbstract: true);

        Assert.Equal(EditOutcome.Applied, session.Apply(ById(session, StructureIntentions.MakeConcreteId)).Outcome);
        Assert.Equal("false", concept.GetOrNull(StructureLanguage.IsAbstractProperty));
        Assert.DoesNotContain("abstract", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGestureOpensTheListAndAppliesNothing()
    {
        // The route the user actually takes: the keystroke asks what can be done here, and the answer is a list -
        // the gesture never applies an entry by itself. (A one-entry list is pinned by OfferKeysTests; what is
        // structure-specific is which entries an interface offers.)
        var (session, concept) = Document(isAbstract: true, isInterface: true);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new OfferAction()).Outcome);

        var offered = Assert.IsType<OfferList>(session.Offered);

        Assert.Equal(StructureIntentions.MakeConceptId, offered.Entries[0].Id);
        Assert.Equal("true", concept.GetOrNull(StructureLanguage.IsInterfaceProperty));

        // Enter on the highlighted entry is what applies it.
        Assert.Equal(EditOutcome.Applied, session.Apply(new AcceptOfferAction()).Outcome);
        Assert.Null(session.Offered);
        Assert.Equal("false", concept.GetOrNull(StructureLanguage.IsInterfaceProperty));
    }

    [Fact]
    public void TheTwoInheritanceEntriesFillTheirOwnSlotAndLeaveTheOtherAlone()
    {
        // One entry per list, and each decides its own: filling `implements` says nothing about `extends`.
        var (session, concept) = Document(isAbstract: false);

        Assert.Equal(EditOutcome.Applied, session.Apply(ById(session, StructureIntentions.ImplementInterfaceId)).Outcome);

        var implemented = Assert.Single(concept.GetChildren(StructureLanguage.ImplementsSlot));

        Assert.Equal(CellAddress.OfSlot(implemented.Id, "conceptKey"), session.Selection.Anchor.Address);
        Assert.Contains(
            "implements",
            LayoutSnapshot.Render(session.Layout, width: 120),
            StringComparison.Ordinal);

        // Both entries stay on offer: an inline list has no Enter of its own, so the entry is how a second entry is
        // written, not only the first.
        session.Select(Selection.At(new CellAddress(concept.Id)));

        Assert.Contains(
            IntentionList.For(session),
            offer => offer.Id == StructureIntentions.ImplementInterfaceId);

        Assert.Contains(
            IntentionList.For(session),
            offer => offer.Id == StructureIntentions.InheritFromId);
    }

    [Fact]
    public void ACaretOnASlotOfTheConceptOffersNothing()
    {
        // The entries are about the concept, so they are offered where the concept's own notation is - its header,
        // its options and its section headings. A property of it is a node of its own, and making an ancestor
        // abstract from there would edit something the list is not about.
        var definition = new DefinitionBuilder();
        var concept = definition.Concept("Rules.Rule", slots: [definition.Property("name", "string")]);

        using var session = new EditorSession(concept, StructureNotations.Create());

        var property = concept.GetChildren(StructureLanguage.PropertiesSlot)[0];

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(new CellAddress(property.Id)));

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void ACaretOnAnEnumerationOffersNothing()
    {
        // An enumeration is a definition of its own, and "make abstract" is about a concept: the list is empty
        // where the definition cannot become abstract, rather than offering an edit that would be refused.
        var definition = new DefinitionBuilder();
        var enumeration = definition.Enumeration("Rules.Outcome", "accept", "review");

        using var session = new EditorSession(enumeration, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);

        session.Select(Selection.At(new CellAddress(enumeration.Id)));

        Assert.Empty(IntentionList.For(session));
    }

    /// <summary>The entry with <paramref name="id"/>, which is how a list of several entries is read.</summary>
    private static Intention ById(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);

    private static (EditorSession Session, Node Concept) Document(bool isAbstract, bool isInterface = false)
    {
        var definition = new DefinitionBuilder();
        var concept = definition.Concept(
            "Rules.Rule",
            slots: [definition.Property("name", "string")],
            isAbstract: isAbstract,
            isInterface: isInterface);

        var session = new EditorSession(definition.Root, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(new CellAddress(concept.Id)));

        return (session, concept);
    }
}
