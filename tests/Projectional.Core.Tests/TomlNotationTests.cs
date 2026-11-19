using Projectional.Core;
using Projectional.Projection;
using Projectional.Toml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>How a TOML document is drawn: the file's own lines, with the value in a cell of its own.</summary>
/// <remarks>
/// The drawing is asserted as text because that is what a reader sees. The interesting cases are the two the cell model
/// had to express: a header with its entries indented under it, and the four delimiters of a string chosen by its
/// <c>kind</c> slot rather than by four concepts.
/// </remarks>
public class TomlNotationTests
{
    [Fact]
    public void ADocumentDrawsItsEntriesOnePerLine()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);

        var first = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        first.Set(TomlLanguage.KeySlot, "name");
        model.NewChild(first, TomlLanguage.ValueSlot, TomlConceptKeys.String)
            .Set(TomlLanguage.TextSlot, "demo");

        var second = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        second.Set(TomlLanguage.KeySlot, "count");
        model.NewChild(second, TomlLanguage.ValueSlot, TomlConceptKeys.Integer)
            .Set(TomlLanguage.TextSlot, "42");

        Assert.Equal("name = \"demo\"\ncount = 42", Render(document));
    }

    [Fact]
    public void ATableDrawsItsHeaderAboveItsIndentedEntries()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);
        var table = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.Table);
        table.Set(TomlLanguage.KeySlot, "server");

        var pair = model.NewChild(table, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, "port");
        model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Integer)
            .Set(TomlLanguage.TextSlot, "8080");

        Assert.Equal("[server]\n  port = 8080", Render(document));
    }

    [Fact]
    public void AnArrayOfTablesDrawsInDoubleBrackets()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);
        var element = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.ArrayOfTables);
        element.Set(TomlLanguage.KeySlot, "cluster");

        // The empty entry list stays visible: it is the caret stop that fills the element.
        Assert.Equal("[[cluster]]\n  empty", Render(document));
    }

    [Fact]
    public void AnEmptyDocumentKeepsItsSlotVisible()
    {
        // One cell, not none: an empty document is where the first entry is typed, so hiding the placeholder would make
        // the only reachable gesture disappear.
        var model = TomlLanguage.NewProject().NewModel("settings");

        Assert.Equal("empty", Render(model.NewNode(TomlConceptKeys.Document)));
    }

    [Fact]
    public void AnArrayAndAnInlineTableDrawTheirBrackets()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var array = model.NewNode(TomlConceptKeys.Array);

        foreach (var literal in new[] { "1", "2" })
        {
            model.NewChild(array, TomlLanguage.ItemsSlot, TomlConceptKeys.Integer)
                .Set(TomlLanguage.TextSlot, literal);
        }

        Assert.Equal("[ 1, 2 ]", Render(array));
        Assert.Equal("{ }", Render(model.NewNode(TomlConceptKeys.InlineTable)));
    }

    [Theory]
    [InlineData(TomlLanguage.BasicString, "\"text\"")]
    [InlineData(TomlLanguage.MultilineBasicString, "\"\"\"text\"\"\"")]
    [InlineData(TomlLanguage.LiteralString, "'text'")]
    [InlineData(TomlLanguage.MultilineLiteralString, "'''text'''")]
    public void AStringDrawsTheDelimitersOfItsKind(string kind, string expected)
    {
        // The delimiters are constants of the notation, so the caret never lands inside one: the slot holds the text
        // between them and that is all the user can edit.
        var model = TomlLanguage.NewProject().NewModel("settings");
        var text = model.NewNode(TomlConceptKeys.String);
        text.Set(TomlLanguage.TextSlot, "text");
        text.Set(TomlLanguage.KindSlot, kind);

        Assert.Equal(expected, Render(text));
    }

    [Fact]
    public void ACommentDrawsItsHashAndItsText()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var comment = model.NewNode(TomlConceptKeys.Comment);
        comment.Set(TomlLanguage.TextSlot, " why");

        Assert.Equal("# why", Render(comment));
    }

    private static string Render(Node node) =>
        LayoutSnapshot.Render(node, TomlLanguage.Notations, width: 120).Trim();
}
