using Projectional.Json;
using Projectional.Language;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>JSON in: the document the file describes, held in the text the file was written in.</summary>
/// <remarks>
/// The interesting half of a reference language is not "the tree is right" - it is that nothing the file says is
/// dropped: a number keeps its literal, a string keeps its escapes, a duplicate member stays duplicated, and a syntax
/// error names the character that broke it. Those are the assertions here; the notation and the generator are pinned
/// in their own files.
/// </remarks>
public sealed class JsonImporterTests
{
    [Fact]
    public void TheDocumentIsTheValueTheFileHas()
    {
        var (model, result) = Import("{ \"a\": 1 }");

        Assert.True(result.Succeeded);
        Assert.Equal(JsonConceptKeys.Object, Assert.Single(model.Roots).Concept.Key);
    }

    [Fact]
    public void AValueAtTheTopLevelNeedsNoWrapperConcept()
    {
        // A JSON document *is* one value, so the root of the model is what the file had - "1" is a document.
        var (model, result) = Import("1");

        Assert.True(result.Succeeded);
        Assert.Equal(JsonConceptKeys.Number, Assert.Single(result.Roots).Concept.Key);
    }

    [Fact]
    public void MembersKeepTheOrderTheFileHas()
    {
        var member = Members("{ \"b\": 1, \"a\": 2, \"c\": 3 }");

        Assert.Equal(["b", "a", "c"], member.Select(m => m.GetOrNull(JsonLanguage.NameSlot)));
    }

    [Fact]
    public void AStringKeepsTheSpellingOfTheFile()
    {
        // Not the decoded value: the escape sequences are what the file says, and a template emits a slot verbatim, so
        // keeping them is what makes the round trip exact without an "as JSON" escaping part.
        var (_, result) = Import("\"a\\nb\\u0041\\\"c\"");

        Assert.Equal("a\\nb\\u0041\\\"c", Assert.Single(result.Roots).GetOrNull(JsonLanguage.TextSlot));
    }

    [Fact]
    public void AMembersNameKeepsTheSpellingOfTheFile()
    {
        var member = Members("{ \"a\\/b\": 1 }");

        Assert.Equal("a\\/b", Assert.Single(member).GetOrNull(JsonLanguage.NameSlot));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.50")]
    [InlineData("1e3")]
    [InlineData("-2.5E-3")]
    [InlineData("123456789012345678901234567890")]
    public void ANumberKeepsTheLiteralTextOfTheFile(string literal)
    {
        // 1, 1.0 and 1e3 are three different files, and a double would rewrite all of them - JSON consumers care,
        // because "1.0" is a float to one reader and an integer to another.
        var (_, result) = Import(literal);

        Assert.Equal(literal, Assert.Single(result.Roots).GetOrNull(JsonLanguage.TextSlot));
    }

    [Fact]
    public void ABooleanIsTheEnumerationTheConceptDeclares()
    {
        var (model, result) = Import("[true, false]");

        var items = Assert.Single(result.Roots).GetChildren(JsonLanguage.ItemsSlot);

        Assert.All(items, item => Assert.Equal(JsonConceptKeys.Bool, item.Concept.Key));
        Assert.Equal(["true", "false"], items.Select(item => item.GetOrNull(JsonLanguage.ValueSlot)));
        Assert.Single(model.Roots);
    }

    [Fact]
    public void NullIsAConceptRatherThanAnAbsentValue()
    {
        var (_, result) = Import("null");

        Assert.Equal(JsonConceptKeys.Null, Assert.Single(result.Roots).Concept.Key);
    }

    [Fact]
    public void NestedValuesAreStoredWhereTheirSlotPoints()
    {
        var (_, result) = Import("{ \"a\": { \"b\": [1, { }] } }");

        var outer = Assert.Single(result.Roots);
        var member = Assert.Single(outer.GetChildren(JsonLanguage.MembersSlot));
        var inner = member.GetChild(JsonLanguage.ValueSlot)!;
        var items = inner.GetChildren(JsonLanguage.MembersSlot);

        Assert.Equal(JsonConceptKeys.Object, inner.Concept.Key);
        Assert.Equal("b", Assert.Single(items).GetOrNull(JsonLanguage.NameSlot));

        var array = items[0].GetChild(JsonLanguage.ValueSlot)!;
        Assert.Equal(JsonConceptKeys.Array, array.Concept.Key);
        Assert.Equal(2, array.GetChildren(JsonLanguage.ItemsSlot).Count);
    }

    [Fact]
    public void ADuplicateMemberIsKeptAndReported()
    {
        // JSON allows it and discourages it; a consumer sees both, so dropping one would rewrite the user's file.
        var (_, result) = Import("{ \"a\": 1, \"a\": 2 }");

        Assert.True(result.Succeeded);
        Assert.Equal(2, Assert.Single(result.Roots).GetChildren(JsonLanguage.MembersSlot).Count);

        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal(JsonImporter.DuplicateName, warning.Code);
        Assert.Equal(ImportSeverity.Warning, warning.Severity);
        Assert.Equal(1, warning.Line);
        Assert.Equal(11, warning.Column);
        Assert.Equal(3, warning.Length);
    }

    [Fact]
    public void TwoSpellingsOfOneNameAreOneName()
    {
        // "\u0061" is "a", so this object names one member twice even though the two tokens differ.
        var (_, result) = Import("{ \"a\": 1, \"\\u0061\": 2 }");

        Assert.Equal(2, Assert.Single(result.Roots).GetChildren(JsonLanguage.MembersSlot).Count);
        Assert.Equal(JsonImporter.DuplicateName, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void TextThatIsNotJsonImportsNothing()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var before = ModelXml.Serialize(model);
        var version = model.Version;

        var result = JsonLanguage.Importer.Import(model, "{ \"a\": 1,");

        Assert.False(result.Succeeded);
        Assert.Empty(model.Roots);
        Assert.Equal(version, model.Version);
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
    public void ThePlaceOfASyntaxErrorNamesTheCharacterThatBrokeIt()
    {
        // The "x" is the eighth character of the line: the tokenizer stops on it, and the diagnostic repeats that place
        // rather than the last token that happened to parse.
        var (_, result) = Import("[1, 2, x]");

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(JsonImporter.UnexpectedToken, error.Code);
        Assert.Equal(1, error.Line);
        Assert.Equal(8, error.Column);
    }

    [Fact]
    public void ThePlaceOfASyntaxErrorCountsLinesOfTheFile()
    {
        var (_, result) = Import("[\n  1,\n  x\n]");

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(3, error.Line);
        Assert.Equal(3, error.Column);
    }

    [Fact]
    public void ACommentIsRefusedRatherThanDropped()
    {
        // JSON has no comments: a JSONC file accepted here would come back stripped on the next save.
        var (_, result) = Import("{ /* note */ \"a\": 1 }");

        Assert.False(result.Succeeded);
        Assert.Equal(JsonImporter.UnexpectedToken, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ASecondTopLevelValueIsRefused()
    {
        var (model, result) = Import("1 2");

        Assert.False(result.Succeeded);
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void AnImportAppendsToANonEmptyModel()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var existing = model.NewNode(JsonConceptKeys.Null);

        var result = JsonLanguage.Importer.Import(model, "1");

        Assert.True(result.Succeeded);
        Assert.Equal(2, model.Roots.Count);
        Assert.Same(existing, model.Roots[0]);
        Assert.Same(result.Roots[0], model.Roots[1]);
    }

    [Fact]
    public void AnImportIsOneUndoStep()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        using var history = new UndoHistory(model);

        JsonLanguage.Importer.Import(model, "{ \"a\": [1, 2, { \"b\": null }] }");

        Assert.Equal(1, history.UndoDepth);

        Assert.True(history.Undo());
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void TheFormatNameIsWhatAFileDialogFiltersOn()
    {
        Assert.Equal("json", JsonLanguage.Importer.Format);
        Assert.True(JsonLanguage.Importer.CanImport("{ }"));
        Assert.True(JsonLanguage.Importer.CanImport("  \r\n  ["));
        Assert.False(JsonLanguage.Importer.CanImport(""));
        Assert.False(JsonLanguage.Importer.CanImport("a: 1"));
    }

    private static IReadOnlyList<Node> Members(string json)
    {
        var (_, result) = Import(json);
        return Assert.Single(result.Roots).GetChildren(JsonLanguage.MembersSlot);
    }

    private static (Model Model, ImportResult Result) Import(string json)
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        return (model, JsonLanguage.Importer.Import(model, json));
    }
}
