using System.Globalization;

using Projectional.Languages;
using Projectional.Testing;
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

        // One row per aspect the framework knows, whether or not this module has it: "no typing rules" is a fact about
        // the module and a row that is missing cannot say it. The count comes from the one list, because a literal
        // here is what let two aspects go unlisted.
        Assert.Equal(LanguageProject.AspectFolders.Count + 1, facts.Aspects.Count);
        Assert.Contains(facts.Aspects, aspect => aspect.Name == "typing" && aspect.Documents == 0 && !aspect.IsPresent);
        // The sample ships an accessory document - a model of itself - so the aspect is present and holds one, which is
        // the row the dialog needs to show rather than hide.
        Assert.Contains(facts.Aspects, aspect => aspect.Name == "accessory" && aspect.Documents == 1 && aspect.IsPresent);
        Assert.Contains(facts.Aspects, aspect => aspect.Name == "structure" && aspect.Documents > 0 && aspect.IsPresent);
    }

    [Fact]
    public void TheAspectRowsAreTheOneListOfAspectFolders()
    {
        // The dialog and the project explorer draw the same folders, so the rows are derived from the list rather
        // than written a second time: everything but the tests folder, in its order.
        var facts = ModuleFacts.Of(Language());

        Assert.Equal(
            [.. LanguageProject.AspectFolders, TestRunner.Folder],
            facts.Aspects.Select(aspect => aspect.Name));
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

        // The accessory document's roots are the language's own concepts, so it belongs in the list; the order is the
        // folder order the dialog and the explorer share.
        Assert.Equal(
            ["structure", "editor", "evaluation", "constraints", "accessory", "import", "generator", "tests"],
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

    [Fact]
    public void TheContentsShowWhatTheLanguageDeclaresAboutItself()
    {
        // The identity page carries the author's two version numbers; these rows carry what `language.mps` says, so a
        // declared version that nothing else reads is still a statement a reader can see.
        var facts = ModuleFacts.Of(Language());

        Assert.Equal("1.0.0", Value(facts, "Declared version"));
        Assert.Equal("none", Value(facts, "Dependencies"));
        Assert.Equal("none", Value(facts, "Extends"));

        // The sample declares nothing to stand on, so nothing resolved; the row is what makes an unanswered
        // declaration visible instead of silent.
        Assert.Equal("none", Value(facts, "Resolved"));
        Assert.Equal("none", Value(facts, "Inherited"));
    }

    private static int Number(ModuleFacts facts, string name) => int.Parse(Value(facts, name), CultureInfo.InvariantCulture);

    private static string Value(ModuleFacts facts, string name) =>
        facts.Contents.Single(fact => fact.Name == name).Value;

    private static LanguageProject Language() =>
        LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error)
        ?? throw new InvalidOperationException(error);
}
