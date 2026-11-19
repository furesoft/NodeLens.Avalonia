using Projectional.Core;
using Projectional.Language;
using Projectional.Semantics;
using Projectional.Hocon;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>HOCON out: the file, spelled by the language that owns the format.</summary>
/// <remarks>
/// The separators, the delimiters and the indentation under a brace are pinned here rather than in the importer's tests,
/// because this is the half that decides what a saved file looks like - and the corpus compares against exactly it.
/// </remarks>
public sealed class HoconTextGeneratorsTests
{
    [Fact]
    public void APropertyIsItsKeyItsSeparatorAndItsValue()
    {
        var model = NewModel();
        var property = Property(model, model.NewNode(HoconConceptKeys.Document), "name", HoconLanguage.EqualsOperator);
        var value = model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.String);
        value.Set(HoconLanguage.TextSlot, "demo");
        value.Set(HoconLanguage.QuotedSlot, HoconLanguage.Quoted);

        Assert.Equal("name = \"demo\"", Generate(property));
    }

    [Theory]
    [InlineData(HoconLanguage.EqualsOperator, "port = 8080")]
    [InlineData(HoconLanguage.ColonOperator, "port: 8080")]
    [InlineData(HoconLanguage.AppendOperator, "port += 8080")]
    [InlineData(HoconLanguage.SpaceOperator, "port 8080")]
    public void EachSeparatorSpellsItself(string @operator, string expected)
    {
        // One property and four conditional parts: a cell behind a condition emits only while its value holds, so the
        // separator a file had is the separator it gets back.
        var model = NewModel();
        var property = Property(model, model.NewNode(HoconConceptKeys.Document), "port", @operator);
        model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Number)
            .Set(HoconLanguage.LiteralSlot, "8080");

        Assert.Equal(expected, Generate(property));
    }

    [Fact]
    public void AnObjectIsItsBracesAroundIndentedMembers()
    {
        var model = NewModel();
        var document = model.NewNode(HoconConceptKeys.Document);
        var property = Property(model, document, "server", HoconLanguage.SpaceOperator);
        var server = model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Object);

        var port = Property(model, server, "port", HoconLanguage.EqualsOperator);
        model.NewChild(port, HoconLanguage.ValueSlot, HoconConceptKeys.Number)
            .Set(HoconLanguage.LiteralSlot, "8080");

        Assert.Equal("server {\n  port = 8080\n}", Generate(document).TrimEnd('\n'));
    }

    [Fact]
    public void AnEmptyObjectIsTwoBraces()
    {
        var model = NewModel();
        var property = Property(model, model.NewNode(HoconConceptKeys.Document), "empty", HoconLanguage.EqualsOperator);
        model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Object);

        Assert.Equal("empty = {}", Generate(property));
    }

    [Fact]
    public void AnArrayIsItsBracketsAroundItsItems()
    {
        var model = NewModel();
        var array = model.NewNode(HoconConceptKeys.Array);

        foreach (var literal in new[] { "1", "2" })
        {
            model.NewChild(array, HoconLanguage.ItemsSlot, HoconConceptKeys.Number)
                .Set(HoconLanguage.LiteralSlot, literal);
        }

        Assert.Equal("[ 1, 2 ]", Generate(array));
    }

    [Fact]
    public void AnEmptyArrayIsTwoBrackets()
    {
        var model = NewModel();

        Assert.Equal("[]", Generate(model.NewNode(HoconConceptKeys.Array)));
    }

    [Theory]
    [InlineData(HoconLanguage.Unquoted, "text")]
    [InlineData(HoconLanguage.Quoted, "\"text\"")]
    [InlineData(HoconLanguage.TripleQuoted, "\"\"\"text\"\"\"")]
    public void EachStringKindSpellsItsOwnDelimiters(string quotedness, string expected)
    {
        var model = NewModel();
        var text = model.NewNode(HoconConceptKeys.String);
        text.Set(HoconLanguage.TextSlot, "text");
        text.Set(HoconLanguage.QuotedSlot, quotedness);

        Assert.Equal(expected, Generate(text));
    }

    [Fact]
    public void AConcatenationJoinsItsPartsWithASpace()
    {
        var model = NewModel();
        var concat = model.NewNode(HoconConceptKeys.Concat);

        var greeting = model.NewChild(concat, HoconLanguage.PartsSlot, HoconConceptKeys.String);
        greeting.Set(HoconLanguage.TextSlot, "hello");
        greeting.Set(HoconLanguage.QuotedSlot, HoconLanguage.Unquoted);

        var name = model.NewChild(concat, HoconLanguage.PartsSlot, HoconConceptKeys.Substitution);
        name.Set(HoconLanguage.PathSlot, "user");

        Assert.Equal("hello ${user}", Generate(concat));
    }

    [Fact]
    public void ASubstitutionSpellsItsQuestionMark()
    {
        var model = NewModel();
        var substitution = model.NewNode(HoconConceptKeys.Substitution);
        substitution.Set(HoconLanguage.PathSlot, "JAVA_HOME");
        substitution.Set(HoconLanguage.OptionalSlot, HoconLanguage.Optional);

        Assert.Equal("${?JAVA_HOME}", Generate(substitution));
    }

    [Fact]
    public void AnIncludeIsTheWordAndItsTarget()
    {
        var model = NewModel();
        var include = model.NewNode(HoconConceptKeys.Include);
        include.Set(HoconLanguage.TargetSlot, "\"other.conf\"");

        Assert.Equal("include \"other.conf\"", Generate(include));
    }

    [Fact]
    public void ACommentKeepsItsMarker()
    {
        var model = NewModel();
        var hash = model.NewNode(HoconConceptKeys.Comment);
        hash.Set(HoconLanguage.TextSlot, " note");

        var slashes = model.NewNode(HoconConceptKeys.Comment);
        slashes.Set(HoconLanguage.MarkerSlot, "//");
        slashes.Set(HoconLanguage.TextSlot, " also a note");

        Assert.Equal("# note", Generate(hash));
        Assert.Equal("// also a note", Generate(slashes));
    }

    [Fact]
    public void ADocumentEndsWithALineBreak()
    {
        var model = NewModel();
        var document = model.NewNode(HoconConceptKeys.Document);
        var property = Property(model, document, "a", HoconLanguage.EqualsOperator);
        model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Null);

        Assert.Equal("a = null\n", Generate(document));
    }

    [Fact]
    public void TheGeneratorIsNamedSoAMenuCanListIt()
    {
        Assert.Equal("HOCON", HoconTextGenerators.Hocon.Name);
        Assert.Same(HoconTextGenerators.Hocon, Assert.Single(HoconTextGenerators.All));
    }

    /// <summary>A property of <paramref name="container"/>, with its key and its separator set.</summary>
    private static Node Property(Model model, Node container, string key, string @operator)
    {
        var property = model.NewChild(container, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        property.Set(HoconLanguage.KeySlot, key);
        property.Set(HoconLanguage.OperatorSlot, @operator);

        return property;
    }

    private static string Generate(Node node)
    {
        var generated = new Generator().Generate(node, HoconTextGenerators.Hocon);

        Assert.True(generated.Succeeded, generated.ToString());
        return generated.Text;
    }

    private static Model NewModel() => HoconLanguage.NewProject().NewModel("application");
}
