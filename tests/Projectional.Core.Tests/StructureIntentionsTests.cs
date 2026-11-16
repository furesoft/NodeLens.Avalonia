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

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal(StructureIntentions.MakeAbstractId, offer.Id);
        Assert.Equal("Make abstract", offer.Label);
        Assert.False(string.IsNullOrWhiteSpace(offer.Description));
        Assert.True(offer.Availability.IsApplicable);
        Assert.Equal(EditSafety.Plain, offer.Safety);
    }

    [Fact]
    public void AnAbstractConceptOffersToBecomeConcrete()
    {
        var (session, _) = Document(isAbstract: true);

        Assert.Equal(StructureIntentions.MakeConcreteId, Assert.Single(IntentionList.For(session)).Id);
    }

    [Fact]
    public void ApplyingItRewritesTheHeaderAndIsOneUndoStep()
    {
        var (session, concept) = Document(isAbstract: false);

        // Before: a concrete concept says only `concept`.
        Assert.Contains("concept Rules.Rule", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
        Assert.DoesNotContain("abstract", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);

        var result = session.Apply(Assert.Single(IntentionList.For(session)));

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
    public void TheOtherDirectionWorksTheSameWay()
    {
        var (session, concept) = Document(isAbstract: true);

        Assert.Equal(EditOutcome.Applied, session.Apply(Assert.Single(IntentionList.For(session))).Outcome);
        Assert.Equal("false", concept.GetOrNull(StructureLanguage.IsAbstractProperty));
        Assert.DoesNotContain("abstract", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGestureRunsTheOnlyOfferWithoutAsking()
    {
        // The route the user actually takes: Alt+Enter with one plain offer applies it and opens no list.
        var (session, concept) = Document(isAbstract: false);

        Assert.Equal(EditOutcome.Applied, session.Apply(new OfferAction()).Outcome);
        Assert.Null(session.Offered);
        Assert.Equal("true", concept.GetOrNull(StructureLanguage.IsAbstractProperty));
    }

    [Fact]
    public void ACaretOnASlotOfTheConceptOffersNothing()
    {
        // The entries are about the concept, so they are offered where the concept's own notation is - its header,
        // its options and its section headings. A property of it is a node of its own, and making an ancestor
        // abstract from there would edit something the list is not about.
        var definition = new DefinitionBuilder();
        var property = definition.Property("name", "string");

        definition.Concept("Rules.Rule", slots: [property]);

        using var session = new EditorSession(definition.Root, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(new CellAddress(property.Id)));

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void ACaretOutsideAConceptOffersNothing()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule");

        using var session = new EditorSession(definition.Root, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);

        // The document root is the definition, not a concept: `enumerations:` and `concepts:` belong to it.
        session.Select(Selection.At(new CellAddress(definition.Root.Id)));

        Assert.Empty(IntentionList.For(session));
    }

    private static (EditorSession Session, Node Concept) Document(bool isAbstract)
    {
        var definition = new DefinitionBuilder();
        var concept = definition.Concept("Rules.Rule", slots: [definition.Property("name", "string")], isAbstract: isAbstract);

        var session = new EditorSession(definition.Root, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(new CellAddress(concept.Id)));

        return (session, concept);
    }
}
