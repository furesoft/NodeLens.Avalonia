using Projectional.Core;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Hocon;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>HOCON in: the document the file describes, held in the text the file was written in.</summary>
/// <remarks>
/// The interesting half of this language is what it deliberately does not do: nothing is merged, nothing is resolved and
/// no include is followed. The assertions are therefore about the text - two repeated keys staying two, a substitution
/// staying a substitution, the separator and the marker as they were written.
/// </remarks>
public sealed class HoconImporterTests
{
    [Fact]
    public void TheDocumentHoldsTheMembersTheFileHas()
    {
        var (_, result) = Import("b = 1\na = 2\n");

        Assert.Equal(["b", "a"], Keys(Assert.Single(result.Roots)));
    }

    [Fact]
    public void ARepeatedKeyIsKeptTwice()
    {
        // HOCON merges them when the file is read; the text has two, and the model owns the text - the opposite of the
        // TOML decision, and both follow from their formats.
        var (_, result) = Import("a = 1\na = 2\n");

        var document = Assert.Single(result.Roots);

        Assert.Equal(["a", "a"], Keys(document));
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("a = 1", HoconLanguage.EqualsOperator)]
    [InlineData("a: 1", HoconLanguage.ColonOperator)]
    [InlineData("a += 1", HoconLanguage.AppendOperator)]
    [InlineData("a 1", HoconLanguage.SpaceOperator)]
    public void TheSeparatorIsKeptAsItWasWritten(string source, string expected)
    {
        var (_, result) = Import(source + "\n");

        Assert.Equal(expected, Assert.Single(result.Roots).GetChildren(HoconLanguage.MembersSlot)[0]
            .GetOrNull(HoconLanguage.OperatorSlot));
    }

    [Theory]
    [InlineData("\"text\"", HoconLanguage.Quoted, "text")]
    [InlineData("\"a\\nb\"", HoconLanguage.Quoted, "a\\nb")]
    [InlineData("plain", HoconLanguage.Unquoted, "plain")]
    [InlineData("\"\"\"one\ntwo\"\"\"", HoconLanguage.TripleQuoted, "one\ntwo")]
    public void AStringKeepsItsSpellingAndItsKind(string source, string quotedness, string text)
    {
        var value = Value(source);

        Assert.Equal(HoconConceptKeys.String, value.Concept.Key);
        Assert.Equal(quotedness, value.GetOrNull(HoconLanguage.QuotedSlot));
        Assert.Equal(text, value.GetOrNull(HoconLanguage.TextSlot));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("-2.5")]
    public void ANumberKeepsTheLiteralTextOfTheFile(string literal)
    {
        var value = Value(literal);

        Assert.Equal(HoconConceptKeys.Number, value.Concept.Key);
        Assert.Equal(literal, value.GetOrNull(HoconLanguage.LiteralSlot));
    }

    [Fact]
    public void TheThreeLiteralsAreTheirOwnConcepts()
    {
        var (_, result) = Import("a = true\nb = false\nc = null\n");

        var members = Assert.Single(result.Roots).GetChildren(HoconLanguage.MembersSlot);

        Assert.Equal(HoconConceptKeys.Bool, members[0].GetChild(HoconLanguage.ValueSlot)!.Concept.Key);
        Assert.Equal("false", members[1].GetChild(HoconLanguage.ValueSlot)!.GetOrNull(HoconLanguage.ValueSlot));
        Assert.Equal(HoconConceptKeys.Null, members[2].GetChild(HoconLanguage.ValueSlot)!.Concept.Key);
    }

    [Fact]
    public void AnObjectIsAValueWithItsOwnMembers()
    {
        var value = Value("{ a = 1 }");

        Assert.Equal(HoconConceptKeys.Object, value.Concept.Key);
        Assert.Equal(["a"], Keys(value));
    }

    [Fact]
    public void ASubstitutionKeepsItsPathAndWhetherItMayBeAbsent()
    {
        var (_, result) = Import("a = ${user.name}\nb = ${?JAVA_HOME}\n");

        var members = Assert.Single(result.Roots).GetChildren(HoconLanguage.MembersSlot);

        var required = members[0].GetChild(HoconLanguage.ValueSlot)!;
        Assert.Equal(HoconConceptKeys.Substitution, required.Concept.Key);
        Assert.Equal("user.name", required.GetOrNull(HoconLanguage.PathSlot));
        Assert.Equal(HoconLanguage.Required, required.GetOrNull(HoconLanguage.OptionalSlot));

        var optional = members[1].GetChild(HoconLanguage.ValueSlot)!;
        Assert.Equal("JAVA_HOME", optional.GetOrNull(HoconLanguage.PathSlot));
        Assert.Equal(HoconLanguage.Optional, optional.GetOrNull(HoconLanguage.OptionalSlot));
    }

    [Fact]
    public void ConcatenationIsItsOwnConceptSoTheSubstitutionInsideItSurvives()
    {
        // Flattening "hello ${user}" into one string would lose the substitution, which is the whole reason HOCON makes
        // juxtaposition meaningful.
        var value = Value("hello ${user}");

        Assert.Equal(HoconConceptKeys.Concat, value.Concept.Key);

        var parts = value.GetChildren(HoconLanguage.PartsSlot);

        Assert.Equal(2, parts.Count);
        Assert.Equal(HoconConceptKeys.String, parts[0].Concept.Key);
        Assert.Equal("hello", parts[0].GetOrNull(HoconLanguage.TextSlot));
        Assert.Equal(HoconConceptKeys.Substitution, parts[1].Concept.Key);
    }

    [Fact]
    public void ASingleValueIsNotAWrappedConcatenation()
    {
        // The concatenation is built first and unwrapped when only one part arrived, so the model of an ordinary value is
        // the value itself.
        Assert.Equal(HoconConceptKeys.String, Value("hello").Concept.Key);
    }

    [Fact]
    public void AnIncludeIsKeptAsWritten()
    {
        var (_, result) = Import("include \"other.conf\"\na = 1\n");

        var members = Assert.Single(result.Roots).GetChildren(HoconLanguage.MembersSlot);

        Assert.Equal(HoconConceptKeys.Include, members[0].Concept.Key);
        Assert.Equal("\"other.conf\"", members[0].GetOrNull(HoconLanguage.TargetSlot));
        Assert.Equal(["a"], Keys(Assert.Single(result.Roots)));
    }

    [Fact]
    public void AnIncludeIsNeverFollowed()
    {
        // The framework takes one string from the host, so a target that does not exist is not an error, not a read and
        // not a diagnostic - it is text the model keeps.
        var (model, result) = Import("include \"does-not-exist.conf\"\na = 1\n");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.Single(model.Roots);
    }

    [Fact]
    public void BothCommentMarkersAreKept()
    {
        var (_, result) = Import("# first\n// second\na = 1\n");

        var members = Assert.Single(result.Roots).GetChildren(HoconLanguage.MembersSlot);

        Assert.Equal(["#", "//"], members.Take(2).Select(member => member.GetOrNull(HoconLanguage.MarkerSlot)));
        Assert.Equal([" first", " second"], members.Take(2).Select(member => member.GetOrNull(HoconLanguage.TextSlot)));
    }

    [Fact]
    public void AnArrayKeepsTheOrderOfItsItems()
    {
        var value = Value("[ 1, \"a\", true ]");

        var items = value.GetChildren(HoconLanguage.ItemsSlot);

        Assert.Equal(
            [HoconConceptKeys.Number, HoconConceptKeys.String, HoconConceptKeys.Bool],
            items.Select(item => item.Concept.Key));
    }

    [Fact]
    public void AnArrayMaySeparateItsItemsWithLineBreaks()
    {
        var value = Value("[\n  1\n  2\n]");

        Assert.Equal(2, value.GetChildren(HoconLanguage.ItemsSlot).Count);
    }

    [Fact]
    public void AKeyKeepsTheSpellingOfTheFile()
    {
        var (_, result) = Import("\"quoted key\" = 1\na.b.c = 2\n");

        Assert.Equal(["\"quoted key\"", "a.b.c"], Keys(Assert.Single(result.Roots)));
    }

    [Fact]
    public void TextThatIsNotHoconImportsNothing()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var before = ModelXml.Serialize(model);
        var version = model.Version;

        var result = HoconLanguage.Importer.Import(model, "a = { b = 1\n");

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
    public void AStrayClosingBraceNamesItsPlace()
    {
        var (_, result) = Import("a = 1\n}\n");

        var error = Assert.Single(result.Diagnostics);

        Assert.Equal(HoconImporter.UnexpectedToken, error.Code);
        Assert.Equal(2, error.Line);
        Assert.Equal(1, error.Column);
    }

    [Fact]
    public void AnImportAppendsToANonEmptyModel()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var existing = model.NewNode(HoconConceptKeys.Document);

        var result = HoconLanguage.Importer.Import(model, "a = 1\n");

        Assert.True(result.Succeeded);
        Assert.Equal(2, model.Roots.Count);
        Assert.Same(existing, model.Roots[0]);
    }

    [Fact]
    public void AnImportIsOneUndoStep()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        using var history = new UndoHistory(model);

        HoconLanguage.Importer.Import(model, "a = 1\nb { c = [ 1, 2 ] }\n");

        Assert.Equal(1, history.UndoDepth);

        Assert.True(history.Undo());
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void TheFormatNameIsWhatAFileDialogFiltersOn()
    {
        Assert.Equal("hocon", HoconLanguage.Importer.Format);
        Assert.True(HoconLanguage.Importer.CanImport("a = 1"));
        Assert.True(HoconLanguage.Importer.CanImport("{ \"a\": 1 }"));
        Assert.True(HoconLanguage.Importer.CanImport("a: 1"));
        Assert.False(HoconLanguage.Importer.CanImport(""));
        Assert.False(HoconLanguage.Importer.CanImport("<a/>"));
    }

    /// <summary>The value of a one-property document, as the importer built it.</summary>
    private static Node Value(string spelled)
    {
        var (_, result) = Import($"v = {spelled}\n");

        return Assert.Single(Assert.Single(result.Roots).GetChildren(HoconLanguage.MembersSlot))
            .GetChild(HoconLanguage.ValueSlot)!;
    }

    private static IEnumerable<string?> Keys(Node container) =>
        container.GetChildren(HoconLanguage.MembersSlot)
            .Where(member => member.Concept.Key == HoconConceptKeys.Property)
            .Select(member => member.GetOrNull(HoconLanguage.KeySlot));

    private static (Model Model, ImportResult Result) Import(string hocon)
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        return (model, HoconLanguage.Importer.Import(model, hocon));
    }
}
