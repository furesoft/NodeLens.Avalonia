using Projectional.Core;
using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a list of proposals says about a concept: its own short name, or the alias the language declared - and the
/// key the entry inserts, which an alias never replaces.
/// </summary>
public sealed class CompletionSourceTests
{
    [Fact]
    public void AConceptIsOfferedByItsAlias()
    {
        var registry = Registry(alias: "Rule");

        var model = new Project(registry).NewModel("Alias");
        var holder = model.NewNode("Rules.RuleSet");

        var offered = CompletionSource.Proposals(registry, holder, "rules");

        // Accepting the entry writes the key, so the alias stays a word of the list. One that reached the model
        // would create a concept nobody declared.
        Assert.Equal("Rule", Assert.Single(offered).Label);
        Assert.Equal("Rules.Rule", offered[0].ConceptKey);
    }

    [Fact]
    public void AConceptWithoutAnAliasIsOfferedByItsName()
    {
        var registry = Registry(alias: null);

        var model = new Project(registry).NewModel("Names");
        var holder = model.NewNode("Rules.RuleSet");

        Assert.Equal("Rule", Assert.Single(CompletionSource.Proposals(registry, holder, "rules")).Label);
    }

    private static ConceptRegistry Registry(string? alias) =>
        ConceptRegistry.Create(
        [
            new ConceptDeclaration("Rules.RuleSet", [new ChildSlot("rules", "Rules.Rule", Cardinality.Many)]),
            new ConceptDeclaration("Rules.Rule", alias: alias),
        ]);
}
