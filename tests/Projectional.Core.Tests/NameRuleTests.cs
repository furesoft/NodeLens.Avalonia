using Projectional.Core;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <see cref="NameRule"/>: what a concept says about the spelling of its name, and how the registry hands it down.
/// </summary>
public sealed class NameRuleTests
{
    [Theory]
    [InlineData("myRule")]
    [InlineData("my_rule2")]
    [InlineData("_private")]
    [InlineData("Rule")]
    [InlineData("")]
    public void AnIdentifierAcceptsAWellFormedName(string name)
    {
        Assert.True(NameRule.Identifier.IsValid(name));
    }

    [Theory]
    [InlineData("my rule")]
    [InlineData("my-rule")]
    [InlineData("2nd")]
    [InlineData("rule.")]
    public void AnIdentifierRefusesWhatIsNotOne(string name)
    {
        Assert.False(NameRule.Identifier.IsValid(name));
    }

    [Fact]
    public void AnIdentifierRefusesALetterThatIsNotAscii()
    {
        // The rule is ASCII on purpose: a generated C# or XML identifier has to carry it. Built from code points
        // so the test source stays ASCII itself.
        var name = "Gr" + char.ConvertFromUtf32(0x00F6) + "sse";

        Assert.False(NameRule.Identifier.IsValid(name));
        Assert.Equal("Gr_sse", NameRule.Identifier.Propose(name));
    }

    [Theory]
    [InlineData("my rule")]
    [InlineData("2nd")]
    [InlineData("Regel")]
    public void AnyTextAcceptsEverything(string name)
    {
        Assert.True(NameRule.AnyText.IsValid(name));
    }

    [Fact]
    public void AnEmptyNameIsNeverASpellingMistake()
    {
        // "Nobody has named this yet" is STD001's business; a rule that refused the empty string would make
        // every unnamed node a second finding about the same absence.
        Assert.True(NameRule.Identifier.IsValid(string.Empty));
    }

    [Fact]
    public void ProposeReplacesIllegalCharactersWithUnderscores()
    {
        Assert.Equal("my_rule", NameRule.Identifier.Propose("my rule"));
        Assert.Equal("my_rule", NameRule.Identifier.Propose("my-rule"));
        Assert.Equal("_2nd", NameRule.Identifier.Propose("2nd"));
        Assert.Equal("Regel", NameRule.Identifier.Propose("Regel"));
    }

    [Fact]
    public void ProposeLeavesALegalNameAlone()
    {
        Assert.Equal("myRule", NameRule.Identifier.Propose("myRule"));
    }

    [Fact]
    public void AnyTextProposesTheNameItWasGiven()
    {
        Assert.Equal("my rule", NameRule.AnyText.Propose("my rule"));
    }

    [Theory]
    [InlineData("any", "any")]
    [InlineData("identifier", "identifier")]
    public void ParseAnswersTheRuleTheWordNames(string word, string expected)
    {
        Assert.Equal(expected, NameRule.Parse(word).Text);
    }

    [Fact]
    public void ParseRefusesAWordThatNamesNoRule()
    {
        var error = Assert.Throws<ConceptDefinitionException>(() => NameRule.Parse("identifer"));

        Assert.Contains("identifer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConceptThatDeclaresNothingHasNoRule()
    {
        var registry = ConceptRegistry.Create([new ConceptDeclaration("Rules.Rule")]);

        Assert.Null(registry.Get("Rules.Rule").NameRule);
    }

    [Fact]
    public void AConceptCarriesTheRuleItDeclares()
    {
        var registry = ConceptRegistry.Create(
            [new ConceptDeclaration("Rules.Rule", nameRule: NameRule.Identifier)]);

        Assert.Same(NameRule.Identifier, registry.Get("Rules.Rule").NameRule);
    }

    [Fact]
    public void ASubtypeInheritsTheRuleOfItsSuper()
    {
        var registry = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Rules.Element", nameRule: NameRule.Identifier),
            new ConceptDeclaration("Rules.Rule", superConcepts: ["Rules.Element"]),
        ]);

        Assert.Same(NameRule.Identifier, registry.Get("Rules.Rule").NameRule);
    }

    [Fact]
    public void ASubtypeCanSwitchTheRuleOffAgain()
    {
        // "any" is a declaration, not an absence: it is how a language says "this branch is prose" below a
        // base that asked for identifiers.
        var registry = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Rules.Element", nameRule: NameRule.Identifier),
            new ConceptDeclaration("Rules.Rule", superConcepts: ["Rules.Element"], nameRule: NameRule.AnyText),
        ]);

        Assert.Same(NameRule.AnyText, registry.Get("Rules.Rule").NameRule);
    }

    [Fact]
    public void AnInterfaceThatDeclaresNothingDoesNotShadowTheRuleBelowIt()
    {
        // Framework.Named declares no rule, so a concept that implements it and declares "identifier" keeps its
        // own. This is why the aspect stays nullable instead of defaulting to AnyText: a default would make the
        // interface answer for every concept that implements it.
        var registry = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Rules.Rule",
                [new PropertySlot("name", PropertyType.String)],
                interfaces: ["Framework.Named"],
                nameSlot: "name",
                nameRule: NameRule.Identifier),
        ]);

        Assert.Same(NameRule.Identifier, registry.Get("Rules.Rule").NameRule);
    }
}
