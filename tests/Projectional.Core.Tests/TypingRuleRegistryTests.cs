using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The registry is the language's answer to "is the text I am typing you?", so these tests hold it to the
/// three promises a language author relies on: adding a rule does not change the registry it was added
/// to, one concept cannot claim text twice, and a concept that declares no rule claims nothing.
/// </summary>
public sealed class TypingRuleRegistryTests
{
    [Fact]
    public void TheEmptyRegistryClaimsNothing()
    {
        Assert.Null(TypingRuleRegistry.Empty.ClaimFor("Test.Step", "1"));
        Assert.Equal("no typing rules", TypingRuleRegistry.Empty.ToString());
    }

    [Fact]
    public void AddReturnsANewRegistryAndLeavesTheOldOneAlone()
    {
        var registry = TypingRuleRegistry.Empty.Add("Test.Step", text => text.Length > 0, "text");

        Assert.NotNull(registry.ClaimFor("Test.Step", "a"));
        Assert.Null(TypingRuleRegistry.Empty.ClaimFor("Test.Step", "a"));
        Assert.Equal("Test.Step", registry.ToString());
    }

    [Fact]
    public void TheRuleRemembersThePropertyTheTextBelongsIn()
    {
        var rule = TypingRuleRegistry.Empty.Add("Test.Step", text => text.Length > 0, "text").ClaimFor("Test.Step", "a");

        Assert.NotNull(rule);
        Assert.Equal("Test.Step", rule.ConceptKey);
        Assert.Equal("text", rule.PropertySlot);
        Assert.Equal("Test.Step.text", rule.ToString());
    }

    [Fact]
    public void AConceptThatDeclaresNoRuleClaimsNothing()
    {
        var registry = TypingRuleRegistry.Empty.Add("Test.Step", text => text.Length > 0, "text");

        Assert.Null(registry.ClaimFor("Test.Rule", "a"));
    }

    [Fact]
    public void AConceptWithARuleStillClaimsOnlyWhatTheRuleAccepts()
    {
        var registry = TypingRuleRegistry.Empty.Add("Test.Step", text => text.All(char.IsAsciiDigit), "index");

        Assert.NotNull(registry.ClaimFor("Test.Step", "12"));
        Assert.Null(registry.ClaimFor("Test.Step", "twelve"));
    }

    [Fact]
    public void ARuleWithoutAPropertyStillClaimsTheText()
    {
        var registry = TypingRuleRegistry.Empty.Add("Test.Body", text => text is "(");

        var rule = registry.ClaimFor("Test.Body", "(");

        Assert.NotNull(rule);
        Assert.Null(rule.PropertySlot);
        Assert.Equal("Test.Body", rule.ToString());
    }

    [Fact]
    public void AddingTheSameConceptTwiceIsRefused()
    {
        var registry = TypingRuleRegistry.Empty.Add("Test.Step", text => true);

        var error = Assert.Throws<ArgumentException>(
            () => registry.Add("Test.Step", text => true));

        Assert.Equal("conceptKey", error.ParamName);
    }

    [Fact]
    public void ToStringNamesTheConceptsInOneOrderHoweverTheyWereAdded()
    {
        var forwards = TypingRuleRegistry.Empty
            .Add("Test.Step", text => true)
            .Add("Test.Rule", text => true);

        var backwards = TypingRuleRegistry.Empty
            .Add("Test.Rule", text => true)
            .Add("Test.Step", text => true);

        Assert.Equal(forwards.ToString(), backwards.ToString());
        Assert.Equal("Test.Rule, Test.Step", forwards.ToString());
    }

    [Fact]
    public void AConceptKeyIsRequired()
    {
        Assert.Throws<ArgumentException>(() => TypingRuleRegistry.Empty.Add(string.Empty, text => true));
        Assert.Throws<ArgumentNullException>(() => TypingRuleRegistry.Empty.Add("Test.Step", null!));
    }
}
