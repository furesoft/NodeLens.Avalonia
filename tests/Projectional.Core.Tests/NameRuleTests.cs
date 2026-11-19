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
    [InlineData("pascal", "pascal")]
    [InlineData("pascal-dot", "pascal-dot")]
    public void ParseAnswersTheRuleTheWordNames(string word, string expected)
    {
        Assert.Equal(expected, NameRule.Parse(word).Text);
    }

    [Fact]
    public void EveryRuleParsesFromTheWordItCarries()
    {
        // The list a declaration enumerates and the list Parse accepts are one list, which is what keeps a
        // definition document from offering a word the lowering would refuse.
        Assert.Equal(11, NameRule.All.Count);

        foreach (var rule in NameRule.All)
        {
            Assert.Same(rule, NameRule.Parse(rule.Text));
        }
    }

    [Theory]
    [InlineData("pascal", "MyRule")]
    [InlineData("camel", "myRule")]
    [InlineData("snake", "my_rule")]
    [InlineData("screaming-snake", "MY_RULE")]
    [InlineData("kebab", "my-rule")]
    [InlineData("screaming-kebab", "MY-RULE")]
    [InlineData("train", "My-Rule")]
    [InlineData("dot", "my.rule")]
    [InlineData("pascal-dot", "My.Rule")]
    public void ACaseRuleAcceptsTheNameItSpells(string word, string name)
    {
        var rule = NameRule.Parse(word);

        Assert.True(rule.IsValid(name));
        Assert.Equal(name, rule.Propose(name));
    }

    [Theory]
    [InlineData("pascal", "myRule")]
    [InlineData("camel", "MyRule")]
    [InlineData("snake", "myRule")]
    [InlineData("screaming-snake", "my_rule")]
    [InlineData("kebab", "MY-RULE")]
    [InlineData("screaming-kebab", "my-rule")]
    [InlineData("train", "my-rule")]
    [InlineData("dot", "My.Rule")]
    [InlineData("pascal-dot", "my.rule")]
    public void ACaseRuleRefusesAnotherConventionsName(string word, string name) =>
        Assert.False(NameRule.Parse(word).IsValid(name));

    [Theory]
    [InlineData("pascal", "my rule", "MyRule")]
    [InlineData("pascal", "HTTP_server", "HttpServer")]
    [InlineData("camel", "my rule", "myRule")]
    [InlineData("snake", "myRule", "my_rule")]
    [InlineData("snake", "ORDER_ID", "order_id")]
    [InlineData("screaming-snake", "myRule", "MY_RULE")]
    [InlineData("screaming-snake", "HTTPServer", "HTTP_SERVER")]
    [InlineData("kebab", "my_rule", "my-rule")]
    [InlineData("train", "my_rule", "My-Rule")]
    [InlineData("dot", "My Rule", "my.rule")]
    [InlineData("pascal-dot", "rules.rule", "Rules.Rule")]
    public void ProposeJoinsTheWordsTheConventionAsksFor(string word, string name, string proposal) =>
        Assert.Equal(proposal, NameRule.Parse(word).Propose(name));

    [Theory]
    [InlineData("camel", "_myField")]
    [InlineData("snake", "_my_field")]
    [InlineData("screaming-snake", "_MY_FIELD")]
    public void ALeadingUnderscoreIsPartOfTheSpelling(string word, string name) =>
        Assert.True(NameRule.Parse(word).IsValid(name), name);

    [Fact]
    public void ANameThatStartsWithADigitGainsAnUnderscore()
    {
        // A case rule is still an identifier rule, so the first character has to be a letter or an underscore -
        // and the fix has to produce something the same rule accepts, which is what makes one repair enough.
        Assert.Equal("_2nd", NameRule.Pascal.Propose("2nd"));
        Assert.True(NameRule.Pascal.IsValid("_2nd"));
        Assert.True(NameRule.Snake.IsValid("_2nd"));
    }

    [Fact]
    public void EveryProposalIsANameItsOwnRuleAccepts()
    {
        // The repair and the test are one algorithm, so this is the property that keeps them honest: a fix may
        // not leave its own finding behind, and repairing something already repaired changes nothing.
        string[] names =
        [
            "my rule",
            "MyRule",
            "my_rule",
            "MY_RULE",
            "my-rule",
            "2nd",
            "HTTPServer",
            " HTTPServer ",
            "Gr" + char.ConvertFromUtf32(0x00F6) + "sse",
            "___",
            "123",
            string.Empty,
        ];

        foreach (var rule in NameRule.All)
        {
            foreach (var name in names)
            {
                var proposal = rule.Propose(name);

                Assert.True(rule.IsValid(proposal), $"{rule.Text}: '{name}' became '{proposal}'");
                Assert.Equal(proposal, rule.Propose(proposal));
            }
        }
    }

    [Fact]
    public void TheWordsOfARuleReadAsTheConventionIsCalled() =>
        Assert.Equal("SCREAMING_SNAKE_CASE", NameRule.ScreamingSnake.Requirement);

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
