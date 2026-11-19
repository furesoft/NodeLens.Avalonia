using Projectional.Core;
using Projectional.Language;
using Projectional.Semantics;
using Projectional.Toml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>TOML out: the file, spelled by the language that owns the format.</summary>
/// <remarks>
/// The delimiter of a string, the indentation under a header and the spaces inside a bracket are pinned here rather
/// than in the importer's tests, because this is the half that decides what a saved file looks like - and the corpus
/// compares against exactly this spelling.
/// </remarks>
public sealed class TomlTextGeneratorsTests
{
    [Fact]
    public void APairIsItsKeyAndItsValue()
    {
        var model = NewModel();
        var document = model.NewNode(TomlConceptKeys.Document);
        var pair = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, "name");
        model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.String)
            .Set(TomlLanguage.TextSlot, "demo");

        Assert.Equal("name = \"demo\"\n", Generate(document));
    }

    [Fact]
    public void ATableHeaderStandsAboveItsEntriesIndented()
    {
        var model = NewModel();
        var document = model.NewNode(TomlConceptKeys.Document);
        var table = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.Table);
        table.Set(TomlLanguage.KeySlot, "server");

        var pair = model.NewChild(table, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, "port");
        model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Integer)
            .Set(TomlLanguage.TextSlot, "8080");

        Assert.Equal("[server]\n  port = 8080\n", Generate(document));
    }

    [Fact]
    public void AnEmptyTableIsItsHeaderAlone()
    {
        var model = NewModel();
        var document = model.NewNode(TomlConceptKeys.Document);
        var table = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.Table);
        table.Set(TomlLanguage.KeySlot, "empty");

        Assert.Equal("[empty]\n", Generate(document));
    }

    [Fact]
    public void AnArrayOfTablesRepeatsItsHeader()
    {
        var model = NewModel();
        var document = model.NewNode(TomlConceptKeys.Document);

        foreach (var node in new[] { "1", "2" })
        {
            var element = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.ArrayOfTables);
            element.Set(TomlLanguage.KeySlot, "cluster");

            var pair = model.NewChild(element, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
            pair.Set(TomlLanguage.KeySlot, "node");
            model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Integer)
                .Set(TomlLanguage.TextSlot, node);
        }

        Assert.Equal("[[cluster]]\n  node = 1\n[[cluster]]\n  node = 2\n", Generate(document));
    }

    [Fact]
    public void AnArrayIsItsBracketsAroundItsItems()
    {
        var model = NewModel();
        var array = model.NewNode(TomlConceptKeys.Array);

        foreach (var literal in new[] { "1", "2" })
        {
            model.NewChild(array, TomlLanguage.ItemsSlot, TomlConceptKeys.Integer)
                .Set(TomlLanguage.TextSlot, literal);
        }

        Assert.Equal("[ 1, 2 ]", Generate(array));
    }

    [Fact]
    public void AnEmptyArrayIsTwoBrackets()
    {
        // Not "[ ]": the gaps exist to separate an item from its brackets, and an empty array has none. The drawing
        // keeps one space, because a slot must stay a caret stop - a drawing is not the file.
        var model = NewModel();

        Assert.Equal("[]", Generate(model.NewNode(TomlConceptKeys.Array)));
    }

    [Fact]
    public void AnInlineTableIsItsBracesAroundItsMembers()
    {
        var model = NewModel();
        var table = model.NewNode(TomlConceptKeys.InlineTable);
        var member = model.NewChild(table, TomlLanguage.MembersSlot, TomlConceptKeys.KeyValue);
        member.Set(TomlLanguage.KeySlot, "x");
        model.NewChild(member, TomlLanguage.ValueSlot, TomlConceptKeys.Integer)
            .Set(TomlLanguage.TextSlot, "1");

        Assert.Equal("{ x = 1 }", Generate(table));
    }

    [Theory]
    [InlineData(TomlLanguage.BasicString, "\"text\"")]
    [InlineData(TomlLanguage.MultilineBasicString, "\"\"\"text\"\"\"")]
    [InlineData(TomlLanguage.LiteralString, "'text'")]
    [InlineData(TomlLanguage.MultilineLiteralString, "'''text'''")]
    public void EachStringKindSpellsItsOwnDelimiters(string kind, string expected)
    {
        var model = NewModel();
        var text = model.NewNode(TomlConceptKeys.String);
        text.Set(TomlLanguage.TextSlot, "text");
        text.Set(TomlLanguage.KindSlot, kind);

        Assert.Equal(expected, Generate(text));
    }

    [Fact]
    public void ACommentIsItsHashAndItsText()
    {
        var model = NewModel();
        var document = model.NewNode(TomlConceptKeys.Document);
        var comment = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.Comment);
        comment.Set(TomlLanguage.TextSlot, " why");

        Assert.Equal("# why\n", Generate(document));
    }

    [Fact]
    public void AnEmptyDocumentIsAnEmptyFile()
    {
        // The trailing line break is conditional on there being an entry: an empty document is not a file with one
        // blank line in it.
        var model = NewModel();

        Assert.Equal(string.Empty, Generate(model.NewNode(TomlConceptKeys.Document)));
    }

    [Fact]
    public void EntriesKeepTheOrderTheModelHas()
    {
        var model = NewModel();
        var document = model.NewNode(TomlConceptKeys.Document);

        var note = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.Comment);
        note.Set(TomlLanguage.TextSlot, " first");

        var pair = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, "a");
        model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Bool)
            .Set(TomlLanguage.ValueSlot, "true");

        Assert.Equal("# first\na = true\n", Generate(document));
    }

    [Fact]
    public void TheGeneratorIsNamedSoAMenuCanListIt()
    {
        Assert.Equal("TOML", TomlTextGenerators.Toml.Name);
        Assert.Same(TomlTextGenerators.Toml, Assert.Single(TomlTextGenerators.All));
    }

    private static string Generate(Node node)
    {
        var generated = new Generator().Generate(node, TomlTextGenerators.Toml);

        Assert.True(generated.Succeeded, generated.ToString());
        return generated.Text;
    }

    private static Model NewModel() => TomlLanguage.NewProject().NewModel("settings");
}
