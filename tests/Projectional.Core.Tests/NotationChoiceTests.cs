using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A choice is the only thing that tells the projection which variant to draw, and it is a value: two
/// views can hold different ones over the same model and neither can change the other.
/// </summary>
public sealed class NotationChoiceTests
{
    [Fact]
    public void TheDefaultChoiceHasNoOpinion()
    {
        Assert.True(NotationChoice.Default.IsEmpty);
        Assert.Null(NotationChoice.Default.VariantOf(TestLanguage.Note));
        Assert.Empty(NotationChoice.Default.Variants);
    }

    [Fact]
    public void AChoiceNamesTheVariantOfTheConceptItWasGiven()
    {
        var choice = NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed);

        Assert.False(choice.IsEmpty);
        Assert.Equal(VariantNotations.Bracketed, choice.VariantOf(TestLanguage.Note));
        Assert.Null(choice.VariantOf(TestLanguage.Body));
    }

    [Fact]
    public void NamingAVariantLeavesTheChoiceItWasBuiltFromAlone()
    {
        var named = NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed);

        Assert.True(NotationChoice.Default.IsEmpty);
        Assert.Single(named.Variants);
    }

    [Fact]
    public void NamingTheSameConceptAgainReplacesTheVariant()
    {
        var choice = NotationChoice.Default
            .With(TestLanguage.Note, VariantNotations.Bracketed)
            .With(TestLanguage.Note, VariantNotations.Unknown);

        Assert.Equal(VariantNotations.Unknown, choice.VariantOf(TestLanguage.Note));
        Assert.Single(choice.Variants);
    }

    [Fact]
    public void ForgettingAConceptLeavesTheOthersNamed()
    {
        var choice = NotationChoice.Default
            .With(TestLanguage.Note, VariantNotations.Bracketed)
            .With(TestLanguage.Body, VariantNotations.Bracketed)
            .Without(TestLanguage.Note);

        Assert.Null(choice.VariantOf(TestLanguage.Note));
        Assert.Equal(VariantNotations.Bracketed, choice.VariantOf(TestLanguage.Body));
    }

    [Fact]
    public void ForgettingAConceptThatWasNeverNamedChangesNothing()
    {
        var choice = NotationChoice.Default.With(TestLanguage.Note, VariantNotations.Bracketed);

        Assert.Equal(choice.VariantOf(TestLanguage.Note), choice.Without(TestLanguage.Body).VariantOf(TestLanguage.Note));
    }

    [Fact]
    public void AChoiceAboutNoConceptSaysSo() => Assert.Equal("no notation choice", NotationChoice.Default.ToString());

    [Fact]
    public void AChoiceListsItsConceptsInKeyOrder() =>
        Assert.Equal(
            "Test.Body=bracketed, Test.Note=bracketed",
            NotationChoice.Default
                .With(TestLanguage.Note, VariantNotations.Bracketed)
                .With(TestLanguage.Body, VariantNotations.Bracketed)
                .ToString());

    [Fact]
    public void AConceptKeyMustNotBeEmpty() =>
        Assert.Throws<ArgumentException>(() => NotationChoice.Default.With(string.Empty, VariantNotations.Bracketed));

    [Fact]
    public void AVariantNameMustNotBeEmpty() =>
        Assert.Throws<ArgumentException>(() => NotationChoice.Default.With(TestLanguage.Note, string.Empty));
}
