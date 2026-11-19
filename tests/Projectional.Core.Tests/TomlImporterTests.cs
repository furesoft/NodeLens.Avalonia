using Projectional.Core;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Toml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>TOML in: the document the file describes, held in the text the file was written in.</summary>
/// <remarks>
/// The interesting half of a reference language is not "the tree is right" - it is that nothing the file says is
/// dropped: a string keeps the kind of delimiter it had, a number keeps its literal, a dotted key stays text, a header
/// keeps its order and a duplicate key is refused. Those are the assertions here; the notation and the generator are
/// pinned in their own files.
/// </remarks>
public sealed class TomlImporterTests
{
    [Fact]
    public void TheDocumentHasTheEntriesTheFileHas()
    {
        var (_, result) = Import("b = 1\na = 2\n");

        var entries = Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot);

        Assert.Equal(["b", "a"], entries.Select(entry => entry.GetOrNull(TomlLanguage.KeySlot)));
    }

    [Fact]
    public void APairBelongsToTheTableWhoseHeaderOpenedIt()
    {
        var (_, result) = Import("root = 1\n[server]\nhost = \"localhost\"\n[server.tls]\nenabled = true\n");

        var entries = Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot);

        Assert.Equal(TomlConceptKeys.KeyValue, entries[0].Concept.Key);

        var table = entries[1];
        Assert.Equal(TomlConceptKeys.Table, table.Concept.Key);
        Assert.Equal("server", table.GetOrNull(TomlLanguage.KeySlot));
        Assert.Equal("host", Assert.Single(table.GetChildren(TomlLanguage.EntriesSlot)).GetOrNull(TomlLanguage.KeySlot));

        // The dotted name is the header's own text, so a nested table is an entry of the document and not of `server`.
        Assert.Equal("server.tls", entries[2].GetOrNull(TomlLanguage.KeySlot));
    }

    [Fact]
    public void AnArrayOfTablesIsOneEntryPerElement()
    {
        var (_, result) = Import("[[cluster]]\nnode = 1\n[[cluster]]\nnode = 2\n");

        var entries = Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot);

        Assert.All(entries, entry => Assert.Equal(TomlConceptKeys.ArrayOfTables, entry.Concept.Key));
        Assert.Equal(2, entries.Count);
    }

    [Theory]
    [InlineData("\"text\"", TomlLanguage.BasicString, "text")]
    [InlineData("\"a\\nb\"", TomlLanguage.BasicString, "a\\nb")]
    [InlineData("'C:\\temp'", TomlLanguage.LiteralString, "C:\\temp")]
    [InlineData("\"\"\"one\ntwo\"\"\"", TomlLanguage.MultilineBasicString, "one\ntwo")]
    [InlineData("'''one\ntwo'''", TomlLanguage.MultilineLiteralString, "one\ntwo")]
    public void AStringKeepsItsKindAndItsSpelling(string source, string kind, string text)
    {
        // The kind is a slot, because 'C:\temp' rewritten as "C:\\temp" is a diff somebody has to explain - and the
        // text is what sat between the delimiters, so a template can emit it verbatim.
        var value = Value("v", source);

        Assert.Equal(TomlConceptKeys.String, value.Concept.Key);
        Assert.Equal(kind, value.GetOrNull(TomlLanguage.KindSlot));
        Assert.Equal(text, value.GetOrNull(TomlLanguage.TextSlot));
    }

    [Theory]
    [InlineData("42", TomlConceptKeys.Integer)]
    [InlineData("0x2A", TomlConceptKeys.Integer)]
    [InlineData("0o52", TomlConceptKeys.Integer)]
    [InlineData("0b101010", TomlConceptKeys.Integer)]
    [InlineData("1_000", TomlConceptKeys.Integer)]
    [InlineData("1.5", TomlConceptKeys.Float)]
    [InlineData("1e3", TomlConceptKeys.Float)]
    [InlineData("inf", TomlConceptKeys.Float)]
    [InlineData("-nan", TomlConceptKeys.Float)]
    public void ANumberKeepsTheLiteralTextOfTheFile(string literal, string conceptKey)
    {
        var value = Value("v", literal);

        Assert.Equal(conceptKey, value.Concept.Key);
        Assert.Equal(literal, value.GetOrNull(TomlLanguage.TextSlot));
    }

    [Theory]
    [InlineData("1979-05-27T07:32:00Z", TomlLanguage.OffsetDateTime)]
    [InlineData("1979-05-27T00:32:00-07:00", TomlLanguage.OffsetDateTime)]
    [InlineData("1979-05-27T07:32:00", TomlLanguage.LocalDateTime)]
    [InlineData("1979-05-27", TomlLanguage.LocalDate)]
    [InlineData("07:32:00", TomlLanguage.LocalTime)]
    public void ADateKeepsItsTextAndItsKind(string literal, string kind)
    {
        var value = Value("v", literal);

        Assert.Equal(TomlConceptKeys.DateTime, value.Concept.Key);
        Assert.Equal(literal, value.GetOrNull(TomlLanguage.TextSlot));
        Assert.Equal(kind, value.GetOrNull(TomlLanguage.KindSlot));
    }

    [Fact]
    public void ABooleanIsTheEnumerationTheConceptDeclares()
    {
        var value = Value("v", "false");

        Assert.Equal(TomlConceptKeys.Bool, value.Concept.Key);
        Assert.Equal("false", value.GetOrNull(TomlLanguage.ValueSlot));
    }

    [Fact]
    public void ADottedKeyStaysTheTextItWasWrittenAs()
    {
        // Resolving "a.b" into nesting would move the pair on the next save; nesting is what a header is for.
        var (_, result) = Import("a.b = 1\n");

        var pair = Assert.Single(Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot));

        Assert.Equal("a.b", pair.GetOrNull(TomlLanguage.KeySlot));
    }

    [Fact]
    public void AQuotedKeyKeepsItsQuotes()
    {
        var (_, result) = Import("\"quoted key\" = 1\n");

        var pair = Assert.Single(Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot));

        Assert.Equal("\"quoted key\"", pair.GetOrNull(TomlLanguage.KeySlot));
    }

    [Fact]
    public void AnInlineTableIsAValueWithMembers()
    {
        var value = Value("v", "{ a = 1, b = \"x\" }");

        Assert.Equal(TomlConceptKeys.InlineTable, value.Concept.Key);
        Assert.Equal(
            ["a", "b"],
            value.GetChildren(TomlLanguage.MembersSlot).Select(member => member.GetOrNull(TomlLanguage.KeySlot)));
    }

    [Fact]
    public void AnArrayKeepsTheOrderOfItsItems()
    {
        var value = Value("v", "[ 1, \"a\", true ]");

        var items = value.GetChildren(TomlLanguage.ItemsSlot);

        Assert.Equal(
            [TomlConceptKeys.Integer, TomlConceptKeys.String, TomlConceptKeys.Bool],
            items.Select(item => item.Concept.Key));
    }

    [Fact]
    public void AnArrayMaySpanLinesAndEndWithAComma()
    {
        var value = Value("v", "[\n  1,\n  2,\n]");

        Assert.Equal(2, value.GetChildren(TomlLanguage.ItemsSlot).Count);
    }

    [Fact]
    public void ACommentIsAnEntryWithoutItsHash()
    {
        // The hash is the notation's, so "# a" and "#a" stay different files.
        var (_, result) = Import("# why\nv = 1\n");

        var entries = Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot);

        Assert.Equal(TomlConceptKeys.Comment, entries[0].Concept.Key);
        Assert.Equal(" why", entries[0].GetOrNull(TomlLanguage.TextSlot));
    }

    [Fact]
    public void ACommentInsideAnArrayIsKeptBesideTheTableAndReported()
    {
        // The model has no entry list inside an array, so the note moves to the table's entries. Keeping the text is
        // the point; the line it lands on is the price, and the warning says so.
        var (_, result) = Import("v = [\n  1, # one\n  2,\n]\n");

        Assert.True(result.Succeeded);

        var entries = Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot);
        var comment = Assert.Single(
            entries,
            entry => string.Equals(entry.Concept.Key, TomlConceptKeys.Comment, StringComparison.Ordinal));

        Assert.Equal(" one", comment.GetOrNull(TomlLanguage.TextSlot));

        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal(TomlImporter.CommentMoved, warning.Code);
        Assert.Equal(ImportSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void ADuplicateKeyImportsNothing()
    {
        // TOML makes a duplicate key a syntax error, and a language does not get to be more permissive than the
        // format it mirrors - so this is a refusal where JSON merely warns.
        var model = TomlLanguage.NewProject().NewModel("settings");
        var before = ModelXml.Serialize(model);
        var version = model.Version;

        var result = TomlLanguage.Importer.Import(model, "a = 1\na = 2\n");

        Assert.False(result.Succeeded);
        Assert.Empty(model.Roots);
        Assert.Equal(version, model.Version);
        Assert.Equal(before, ModelXml.Serialize(model));

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(TomlImporter.DuplicateKey, error.Code);
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void ALegalArrayOfTablesDoesNotCountAsADuplicate()
    {
        var (_, result) = Import("[[a]]\nx = 1\n[[a]]\nx = 2\n");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void TextThatIsNotTomlImportsNothing()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var before = ModelXml.Serialize(model);

        var result = TomlLanguage.Importer.Import(model, "a = \n");

        Assert.False(result.Succeeded);
        Assert.Empty(model.Roots);
        Assert.Equal(before, ModelXml.Serialize(model));
        Assert.Equal(ImportSeverity.Error, Assert.Single(result.Diagnostics).Severity);
    }

    [Fact]
    public void AnEmptyTextIsRefused()
    {
        var (model, result) = Import("  \n  ");

        Assert.False(result.Succeeded);
        Assert.Empty(model.Roots);
        Assert.Equal(1, Assert.Single(result.Diagnostics).Line);
    }

    [Fact]
    public void ThePlaceOfASyntaxErrorCountsLinesOfTheFile()
    {
        var (_, result) = Import("title = \"a\"\n[broken\ntitle = 2\n");

        var error = Assert.Single(result.Diagnostics);

        Assert.Equal(TomlImporter.UnexpectedToken, error.Code);
        Assert.True(error.Line >= 2);
    }

    [Fact]
    public void AnImportAppendsToANonEmptyModel()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var existing = model.NewNode(TomlConceptKeys.Document);

        var result = TomlLanguage.Importer.Import(model, "a = 1\n");

        Assert.True(result.Succeeded);
        Assert.Equal(2, model.Roots.Count);
        Assert.Same(existing, model.Roots[0]);
        Assert.Same(result.Roots[0], model.Roots[1]);
    }

    [Fact]
    public void AnImportIsOneUndoStep()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        using var history = new UndoHistory(model);

        TomlLanguage.Importer.Import(model, "a = 1\n[b]\nc = [ 1, { d = true } ]\n");

        Assert.Equal(1, history.UndoDepth);

        Assert.True(history.Undo());
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void TheFormatNameIsWhatAFileDialogFiltersOn()
    {
        Assert.Equal("toml", TomlLanguage.Importer.Format);
        Assert.True(TomlLanguage.Importer.CanImport("a = 1"));
        Assert.True(TomlLanguage.Importer.CanImport("# note\n[t]\n"));
        Assert.False(TomlLanguage.Importer.CanImport(""));
        Assert.False(TomlLanguage.Importer.CanImport("a: 1"));
        Assert.False(TomlLanguage.Importer.CanImport("{ \"a\": 1 }"));
    }

    /// <summary>The value of a one-pair document, as the importer built it.</summary>
    private static Node Value(string key, string spelled)
    {
        var (_, result) = Import($"{key} = {spelled}\n");

        var pair = Assert.Single(Assert.Single(result.Roots).GetChildren(TomlLanguage.EntriesSlot));

        return pair.GetChild(TomlLanguage.ValueSlot)!;
    }

    private static (Model Model, ImportResult Result) Import(string toml)
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        return (model, TomlLanguage.Importer.Import(model, toml));
    }
}
