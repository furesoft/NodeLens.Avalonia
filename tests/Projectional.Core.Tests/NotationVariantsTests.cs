using Projectional.Core;
using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A concept may be drawn more than one way. The registry keeps every variant, refuses two notations
/// that claim the same name, and answers a reader's choice without deciding anything itself.
/// </summary>
public sealed class NotationVariantsTests
{
    [Fact]
    public void AConceptMayHaveASecondNotation()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var defaultNotation = notations.Add(TestLanguage.Note, ed => ed.Line(ed.Property("text")));
        var bracketed = notations.Add(TestLanguage.Note, "bracketed", ed => ed.Line(ed.Punct("("), ed.Property("text"), ed.Punct(")")));

        Assert.Equal(Notation.DefaultVariant, defaultNotation.Variant);
        Assert.Equal("bracketed", bracketed.Variant);
        Assert.Equal([defaultNotation, bracketed], notations.Notations);
    }

    [Fact]
    public void TheVariantsOfAConceptAreKeptInTheOrderTheyWereDeclared() =>
        Assert.Equal(["default", "bracketed"], VariantNotations.Registry.VariantsOf(TestLanguage.Note));

    [Fact]
    public void AConceptWithoutNotationHasNoVariants() =>
        Assert.Empty(VariantNotations.Registry.VariantsOf(TestLanguage.Tag));

    [Fact]
    public void ANamedVariantIsFoundByItsName()
    {
        Assert.True(VariantNotations.Registry.TryGet(TestLanguage.Note, VariantNotations.Bracketed, out var notation));
        Assert.Equal(VariantNotations.Bracketed, notation.Variant);
    }

    [Fact]
    public void AskingForAVariantTheConceptDoesNotHaveFindsNothing() =>
        Assert.False(VariantNotations.Registry.TryGet(TestLanguage.Note, VariantNotations.Unknown, out _));

    [Fact]
    public void AskingForANamedVariantNobodyDeclaredThrows()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() =>
            VariantNotations.Registry.Get(TestLanguage.Note, VariantNotations.Unknown));

        Assert.Contains("'hologram'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Test.Note'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoChoiceEveryConceptIsDrawnInItsDefaultVariant() =>
        Assert.Equal(
            Notation.DefaultVariant,
            VariantNotations.Registry.FindFor(TestLanguage.Registry.Get(TestLanguage.Note))?.Variant);

    [Fact]
    public void TheChosenVariantIsUsedForTheConceptItNames()
    {
        var choice = NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed);

        var notation = VariantNotations.Registry.FindFor(TestLanguage.Registry.Get(TestLanguage.Note), choice);

        Assert.Equal(VariantNotations.Bracketed, notation?.Variant);
    }

    [Fact]
    public void AChoiceAboutOneConceptLeavesTheOthersAlone()
    {
        var choice = NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed);

        var notation = VariantNotations.Registry.FindFor(TestLanguage.Registry.Get(TestLanguage.Body), choice);

        Assert.Equal(Notation.DefaultVariant, notation?.Variant);
    }

    [Fact]
    public void AChosenVariantIsInheritedLikeADefaultOne()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Base", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Test.Middle", superConcepts: ["Test.Base"]),
            new ConceptDeclaration("Test.Leaf", superConcepts: ["Test.Middle"]),
        ]);
        var notations = new NotationRegistry(concepts);
        var baseDefault = notations.Add("Test.Base", ed => ed.Line(ed.Property("name")));
        var baseTable = notations.Add("Test.Base", "table", ed => ed.Line(ed.Punct("|"), ed.Property("name")));
        notations.Add("Test.Leaf", ed => ed.Line(ed.Keyword("leaf")));

        var leaf = concepts.Get("Test.Leaf");
        var choice = NotationChoice.Default.With("Test.Leaf", "table");

        // The leaf's own default must not shadow the variant the reader asked for.
        Assert.Same(baseTable, notations.FindFor(leaf, choice));
        Assert.NotSame(baseDefault, notations.FindFor(leaf, choice));
    }

    [Fact]
    public void AVariantNobodyDeclaredFallsBackToTheConceptsOwnDefault()
    {
        var choice = NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Unknown);

        var notation = VariantNotations.Registry.FindFor(TestLanguage.Registry.Get(TestLanguage.Note), choice);

        Assert.Equal(Notation.DefaultVariant, notation?.Variant);
    }

    [Fact]
    public void AConceptThatOnlyDeclaresVariantIsDrawnWithItRatherThanNothing()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);
        var only = notations.Add(TestLanguage.Note, "bracketed", ed => ed.Line(ed.Punct("("), ed.Property("text"), ed.Punct(")")));

        Assert.Same(only, notations.FindFor(TestLanguage.Registry.Get(TestLanguage.Note)));
        Assert.Same(only, notations.FindFor(TestLanguage.Registry.Get(TestLanguage.Note), NotationChoice.Default));
    }

    [Fact]
    public void AVariantNameMustNotBeEmpty() =>
        Assert.Throws<ArgumentException>(() =>
            new NotationRegistry(TestLanguage.Registry).Add(TestLanguage.Note, string.Empty, ed => ed.Line(ed.Property("text"))));

    [Fact]
    public void ANotationSaysWhichVariantItIs() =>
        Assert.Equal("Test.Note notation (bracketed)", VariantNotations.Registry.Get(TestLanguage.Note, VariantNotations.Bracketed).ToString());
}
