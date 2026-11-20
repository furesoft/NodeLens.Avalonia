using System.Globalization;

using Projectional.Languages;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a module's settings dialog shows is derived, not stored: the aspects from the folders on disk and the contents
/// from the language the shell built. Both are asserted here because both are invisible from the outside of a module.
/// </summary>
public sealed class ModuleFactsTests
{
    [Fact]
    public void EveryAspectIsListedEvenTheOnesTheModuleDoesNotHave()
    {
        var facts = ModuleFacts.Of(Language());

        // Nine aspects, because "no constraints" is a fact about the module and a row that is missing cannot say it.
        Assert.Equal(9, facts.Aspects.Count);
        Assert.Contains(facts.Aspects, aspect => aspect.Name == "scopes" && aspect.Documents == 0 && !aspect.IsPresent);
        Assert.Contains(facts.Aspects, aspect => aspect.Name == "structure" && aspect.Documents > 0 && aspect.IsPresent);
    }

    [Fact]
    public void AnAspectNamesTheLanguageItIsWrittenIn()
    {
        var facts = ModuleFacts.Of(Language());

        var editor = facts.Aspects.Single(aspect => aspect.Name == "editor");

        Assert.Equal("Editor", editor.Language);
    }

    [Fact]
    public void TheRootsAreTheAspectsThatHoldDocuments()
    {
        var facts = ModuleFacts.Of(Language());

        Assert.Equal(
            ["structure", "editor", "constraints", "import", "generator", "tests"],
            facts.Roots);
    }

    [Fact]
    public void TheContentsSayWhatTheShellBuiltFromTheDefinitions()
    {
        var facts = ModuleFacts.Of(Language());

        Assert.True(Number(facts, "Declared concepts") > 0);
        Assert.True(Number(facts, "Notations") > 0);
        Assert.Equal(1, Number(facts, "Generators"));

        // The sample declares no intentions, and a zero there would be a number nobody asked about.
        Assert.Equal("none", Value(facts, "Intentions"));
    }

    private static int Number(ModuleFacts facts, string name) => int.Parse(Value(facts, name), CultureInfo.InvariantCulture);

    private static string Value(ModuleFacts facts, string name) =>
        facts.Contents.Single(fact => fact.Name == name).Value;

    private static LanguageProject Language() =>
        LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error)
        ?? throw new InvalidOperationException(error);
}
