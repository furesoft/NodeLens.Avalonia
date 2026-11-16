using Projectional.Core;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The declaration itself: a concept that names the places it may stand in, and the registration check that keeps
/// a place from naming something that is not there.
/// </summary>
public sealed class ApplicabilityTests
{
    [Fact]
    public void AConceptThatConstrainsNothingStandsAnywhere()
    {
        var anywhere = Applicability.Anywhere;

        Assert.True(anywhere.IsAnywhere);
        Assert.Empty(anywhere.Places);
        Assert.True(anywhere.Allows(parent: null, slot: null));
        Assert.True(anywhere.Allows(Class, "members"));
        Assert.Equal("anywhere", anywhere.ToString());
    }

    [Fact]
    public void APlaceMatchesTheNamedConceptAndEverythingBelowIt()
    {
        // Comment names the abstract Member, so it may stand wherever a member declares the slot - which is how
        // "anywhere inside a declaration" is written without listing every declaration.
        var comment = ConditionalLanguage.Registry.Get(ConditionalLanguage.Comment);

        Assert.True(comment.Applicability.Allows(ConditionalLanguage.Registry.Get(ConditionalLanguage.Local), "annotations"));
        Assert.True(comment.Applicability.Allows(ConditionalLanguage.Registry.Get(ConditionalLanguage.Class), "annotations"));

        // ...but not at the top, where the parent is not a member.
        Assert.False(comment.Applicability.Allows(ConditionalLanguage.Registry.Get(ConditionalLanguage.Document), "annotations"));
        Assert.False(comment.Applicability.Allows(parent: null, slot: null));
    }

    [Fact]
    public void APlaceThatNamesASlotIsOnlyThatSlot()
    {
        var annotation = ConditionalLanguage.Registry.Get(ConditionalLanguage.Annotation);
        var document = ConditionalLanguage.Registry.Get(ConditionalLanguage.Document);

        Assert.True(annotation.Applicability.Allows(document, "annotations"));
        Assert.False(annotation.Applicability.Allows(document, "members"));
    }

    [Fact]
    public void ConstrainingNowhereIsRefused()
    {
        // A concept that may stand nowhere cannot exist; the language should simply not declare it.
        Assert.Throws<ArgumentException>(() => Applicability.Only());
        Assert.Throws<ArgumentException>(() => Applicability.Only([]));
    }

    [Fact]
    public void PlacesArePrintedSoAMessageCanNameThem()
    {
        Assert.Equal("Conditional.Document.annotations, Conditional.Class.annotations", ConditionalLanguage.Registry.Get(ConditionalLanguage.Annotation).Applicability.ToString());
        Assert.Equal("Conditional.Document", new ConceptPlace(ConditionalLanguage.Document).ToString());
    }

    [Fact]
    public void AConceptReachingForSomethingThatIsNotThereIsRefusedAtRegistration()
    {
        // Registration is where a language error belongs: a place that names an unknown concept, a property
        // instead of a child slot, or a slot of the wrong type would otherwise be a concept that never appears.
        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.A", applicability: Applicability.Only(new ConceptPlace("Test.Nope"))),
        ]));

        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.A", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Test.B", applicability: Applicability.Only(new ConceptPlace("Test.A", "name"))),
        ]));

        Assert.Throws<ConceptDefinitionException>(() => ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.A", [new ChildSlot("body", "Test.B", Cardinality.One)]),
            new ConceptDeclaration("Test.B"),
            new ConceptDeclaration("Test.C", applicability: Applicability.Only(new ConceptPlace("Test.A", "body"))),
        ]));
    }

    [Fact]
    public void TheDefaultConceptForASlotSkipsOneThatMayNotStandThere()
    {
        var concepts = ConditionalLanguage.Registry;
        var local = concepts.Get(ConditionalLanguage.Local);
        var document = concepts.Get(ConditionalLanguage.Document);
        var annotation = ConditionalLanguage.Annotation;

        // Annotation is declared first and is a legal child of the slot by type, but not on a local - so the
        // next candidate answers instead of the model refusing what the editor just created.
        Assert.Equal(
            ConditionalLanguage.Comment,
            concepts.DefaultInstantiableFor(annotation, local, ConditionalLanguage.Annotations)?.Key);

        Assert.Equal(
            annotation,
            concepts.DefaultInstantiableFor(annotation, document, ConditionalLanguage.Annotations)?.Key);
    }

    [Fact]
    public void TheDefaultConceptAnswersNothingWhenNoCandidateFits()
    {
        var concepts = ConditionalLanguage.Registry;
        var local = concepts.Get(ConditionalLanguage.Local);

        // Only a document takes an annotation, and this is not one.
        Assert.Null(concepts.DefaultInstantiableFor(ConditionalLanguage.Annotation, local, ConditionalLanguage.Members));
    }

    [Fact]
    public void APlaceMayNameAConceptRatherThanASlot()
    {
        // Naming no slot means "any slot of this concept", and it is checked the same way.
        Assert.Equal(
            "Test.A",
            new ConceptPlace("Test.A").ToString());

        var registry = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.A", [new ChildSlot("body", "Test.B", Cardinality.One)]),
            new ConceptDeclaration("Test.B", applicability: Applicability.Only(new ConceptPlace("Test.A"))),
        ]);

        Assert.True(registry.Get("Test.B").Applicability.Allows(registry.Get("Test.A"), "body"));
        Assert.False(registry.Get("Test.B").Applicability.Allows(parent: null, slot: null));
    }

    [Fact]
    public void AConceptWithoutAConstraintIsPrintedAsAnywhere()
    {
        Assert.True(ConceptRegistry.Create([new ConceptDeclaration("Test.A")]).Get("Test.A").Applicability.IsAnywhere);
    }

    private static Concept Class => ConditionalLanguage.Registry.Get(ConditionalLanguage.Class);
}
