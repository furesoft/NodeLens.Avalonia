using Projectional.Core;
using Projectional.Language;
using Projectional.TypingRules;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A declared typing rule is the predicate a language would have written in C# - and a wrong one is refused.</summary>
/// <remarks>
/// The concepts are built here rather than read from a folder, because a lowering test is about the rule and not about
/// the loader: <c>LanguageProjectTypingTests</c> is the one that covers the folder.
/// </remarks>
public sealed class TypingLoweringTests
{
    [Fact]
    public void ADigitsRuleClaimsDigitsAndWritesThemIntoItsProperty()
    {
        var rules = Lower(Rule(conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "value"));

        var rule = Assert.IsType<TypingRule>(rules.ClaimFor("Doc.Number", "123"));

        Assert.Equal("Doc.Number", rule.ConceptKey);
        Assert.Equal("value", rule.PropertySlot);
        Assert.Null(rules.ClaimFor("Doc.Number", "12a"));

        // Empty text claims nothing in practice, but it is what the C# twin answers (`"".All(...)` is true), so the
        // declared rule has to answer the same - equality of the two paths is the point of the aspect, not politeness.
        Assert.NotNull(rules.ClaimFor("Doc.Number", string.Empty));
    }

    [Fact]
    public void ANameRuleClaimsTheShapeANameSlotAccepts()
    {
        var rules = Lower(Rule(conceptKey: "Doc.Name", claims: TypingClaims.Name, property: "name"));

        Assert.NotNull(rules.ClaimFor("Doc.Name", "total"));
        Assert.NotNull(rules.ClaimFor("Doc.Name", "_x1"));

        Assert.Null(rules.ClaimFor("Doc.Name", "1x"));
        Assert.Null(rules.ClaimFor("Doc.Name", "a b"));
        Assert.Null(rules.ClaimFor("Doc.Name", string.Empty));
    }

    [Fact]
    public void APrefixRuleClaimsTheTextsThatStartWithIt()
    {
        var rules = Lower(Rule(conceptKey: "Doc.Quoted", claims: TypingClaims.Prefix, text: "\"", property: "value"));

        Assert.NotNull(rules.ClaimFor("Doc.Quoted", "\"abc"));
        Assert.NotNull(rules.ClaimFor("Doc.Quoted", "\""));

        Assert.Null(rules.ClaimFor("Doc.Quoted", "abc"));
    }

    [Fact]
    public void AWordListRuleClaimsExactlyItsWords()
    {
        var rules = Lower(Rule(conceptKey: "Doc.Truth", claims: TypingClaims.AnyOf, text: "true false", property: "value"));

        Assert.NotNull(rules.ClaimFor("Doc.Truth", "true"));
        Assert.NotNull(rules.ClaimFor("Doc.Truth", "false"));

        Assert.Null(rules.ClaimFor("Doc.Truth", "True"));
        Assert.Null(rules.ClaimFor("Doc.Truth", "truely"));
    }

    [Fact]
    public void AConceptWithoutAPropertyCanStillClaim()
    {
        var rules = Lower(Rule(conceptKey: "Doc.Group", claims: TypingClaims.Prefix, text: "("));

        var rule = Assert.IsType<TypingRule>(rules.ClaimFor("Doc.Group", "("));

        Assert.Null(rule.PropertySlot);
    }

    [Fact]
    public void ARuleThatNamesAConceptTheLanguageDoesNotHaveIsRefused()
    {
        var refusal = Refuse(Rule(conceptKey: "Doc.Missing", claims: TypingClaims.Digits));

        Assert.Contains("not a concept of this language", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleThatNamesAChildOrReferenceAsItsPropertyIsRefused()
    {
        var refusal = Refuse(Rule(conceptKey: "Doc.Number", claims: TypingClaims.Digits, property: "children"));

        Assert.Contains("declares no property named 'children'", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AClaimThatReadsNoTextCarryingOneIsRefused()
    {
        var refusal = Refuse(Rule(conceptKey: "Doc.Number", claims: TypingClaims.Digits, text: "123"));

        Assert.Contains("reads no text", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void APrefixRuleWithoutAPrefixIsRefused()
    {
        var refusal = Refuse(Rule(conceptKey: "Doc.Quoted", claims: TypingClaims.Prefix));

        Assert.Contains("starts with nothing", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AWordListWithoutWordsIsRefused()
    {
        var refusal = Refuse(Rule(conceptKey: "Doc.Truth", claims: TypingClaims.AnyOf));

        Assert.Contains("names no word", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownClaimCannotEvenBeWritten()
    {
        // The vocabulary is closed by the property's type, so the refusal is earlier than the lowering: a model cannot
        // hold a claim the language does not have, and a hand-edited file is refused when it loads.
        var model = TypingLanguage.NewModel("rule");
        var rule = model.Roots[0];

        rule.Set(TypingLanguage.ConceptKeySlot, "Doc.Number");

        var refusal = Assert.Throws<ModelInvariantException>(() => rule.Set(TypingLanguage.ClaimsSlot, "sounds like"));

        Assert.Contains("is not a valid enum", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoRulesForOneConceptAreRefused()
    {
        var documents = new[]
        {
            Rule(conceptKey: "Doc.Number", claims: TypingClaims.Digits),
            Rule(conceptKey: "Doc.Number", claims: TypingClaims.Name),
        };

        var refusal = Assert.Throws<ConceptDefinitionException>(() => TypingLowering.CreateRules(documents, Concepts()));

        Assert.Contains("Two typing rules claim 'Doc.Number'", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentThatIsNotARuleIsRefused()
    {
        var model = new Project(Concepts()).NewModel("elsewhere");
        var document = model.NewNode("Doc.Number");

        var refusal = Assert.Throws<ConceptDefinitionException>(() => TypingLowering.CreateRules([document], Concepts()));

        Assert.Contains("is not a typing rule", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoDocumentsIsNoRules()
    {
        var rules = TypingLowering.CreateRules([], Concepts());

        Assert.Same(TypingRuleRegistry.Empty, rules);
    }

    [Fact]
    public void AnAliasRuleClaimsTheWordTheConceptDeclares()
    {
        var rules = Lower(Rule(conceptKey: "Doc.Number", claims: TypingClaims.Alias));

        Assert.NotNull(rules.ClaimFor("Doc.Number", "num"));
        Assert.Null(rules.ClaimFor("Doc.Number", "number"));
    }

    [Fact]
    public void AnAliasRuleOnAConceptWithoutOneIsRefused()
    {
        var refusal = Refuse(Rule(conceptKey: "Doc.Name", claims: TypingClaims.Alias));

        Assert.Contains("declares none", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAliasRuleThatSpellsItsOwnWordIsRefused()
    {
        // Two places to say the word would drift the moment the concept's alias is renamed.
        var refusal = Refuse(Rule(conceptKey: "Doc.Number", claims: TypingClaims.Alias, text: "num"));

        Assert.Contains("reads no text", refusal, StringComparison.Ordinal);
    }

    /// <summary>One rule document, written the way an author would.</summary>
    private static Node Rule(string conceptKey, string claims, string? text = null, string? property = null)
    {
        var model = TypingLanguage.NewModel("rule");
        var rule = model.Roots[0];

        rule.Set(TypingLanguage.ConceptKeySlot, conceptKey);
        rule.Set(TypingLanguage.ClaimsSlot, claims);

        if (text is not null)
        {
            rule.Set(TypingLanguage.TextSlot, text);
        }

        if (property is not null)
        {
            rule.Set(TypingLanguage.PropertySlotSlot, property);
        }

        return rule;
    }

    private static TypingRuleRegistry Lower(params Node[] documents) => TypingLowering.CreateRules(documents, Concepts());

    private static string Refuse(params Node[] documents)
    {
        var refusal = Assert.Throws<ConceptDefinitionException>(() => Lower(documents));

        return refusal.Message;
    }

    /// <summary>The concepts those rules are allowed to name.</summary>
    private static ConceptRegistry Concepts() =>
        ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Doc.Number",
                [new PropertySlot("value", PropertyType.String), new ChildSlot("children", "Doc.Number", Cardinality.Many)],
                alias: "num",
                description: "A number."),
            new ConceptDeclaration("Doc.Name", [new PropertySlot("name", PropertyType.String)], description: "A name."),
            new ConceptDeclaration("Doc.Quoted", [new PropertySlot("value", PropertyType.String)], description: "A quoted value."),
            new ConceptDeclaration("Doc.Truth", [new PropertySlot("value", PropertyType.String)], description: "A truth value."),
            new ConceptDeclaration("Doc.Group", description: "A group with no property."),
        ]);
}
