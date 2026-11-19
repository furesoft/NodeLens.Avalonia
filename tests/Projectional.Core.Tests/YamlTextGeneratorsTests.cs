using Projectional.Core;
using Projectional.Semantics;
using Projectional.Yaml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>YAML out: the file, spelled by the language that owns the format.</summary>
/// <remarks>
/// The indentation, the dashes and the two placements of a pair's value are pinned here rather than in the importer's
/// tests, because this is the half that decides what a saved file looks like - and the corpus compares against exactly it.
/// </remarks>
public sealed class YamlTextGeneratorsTests
{
    [Fact]
    public void AMappingWritesOnePairPerLine()
    {
        var model = NewModel();
        var mapping = model.NewNode(YamlConceptKeys.Mapping);
        Pair(model, mapping, "host", "localhost");
        Pair(model, mapping, "port", "8080");

        Assert.Equal("host: localhost\nport: 8080", Generate(mapping));
    }

    [Fact]
    public void ASequenceWritesOneDashPerItem()
    {
        var model = NewModel();
        var sequence = model.NewNode(YamlConceptKeys.Sequence);

        foreach (var text in new[] { "one", "two" })
        {
            var item = model.NewChild(sequence, YamlLanguage.ItemsSlot, YamlConceptKeys.SequenceItem);
            model.NewChild(item, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, text);
        }

        Assert.Equal("- one\n- two", Generate(sequence));
    }

    [Fact]
    public void ANestedSequenceIndentsUnderItsDash()
    {
        // `- - one` is how a sequence inside an item is written, so the inner dash continues the line rather than starting a
        // column of its own.
        var model = NewModel();
        var outer = model.NewNode(YamlConceptKeys.Sequence);
        var item = model.NewChild(outer, YamlLanguage.ItemsSlot, YamlConceptKeys.SequenceItem);
        var inner = model.NewChild(item, YamlLanguage.ValueSlot, YamlConceptKeys.Sequence);

        foreach (var text in new[] { "one", "two" })
        {
            var nested = model.NewChild(inner, YamlLanguage.ItemsSlot, YamlConceptKeys.SequenceItem);
            model.NewChild(nested, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, text);
        }

        Assert.Equal("- - one\n  - two", Generate(outer));
    }

    [Fact]
    public void APairWritesItsValueOnItsOwnLineWhenTheModelSaysNested()
    {
        var model = NewModel();
        var mapping = model.NewNode(YamlConceptKeys.Mapping);
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "server");

        var value = model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Mapping);
        Pair(model, value, "port", "8080");
        pair.Set(YamlLanguage.LayoutSlot, YamlLanguage.Nested);

        Assert.Equal("server:\n  port: 8080", Generate(mapping));
    }

    [Fact]
    public void APairKeepsAnInlineValueOnTheKeyLine()
    {
        var model = NewModel();
        var mapping = model.NewNode(YamlConceptKeys.Mapping);
        Pair(model, mapping, "a", "1");

        Assert.Equal("a: 1", Generate(mapping));
    }

    [Fact]
    public void APairWithoutAValueWritesItsColonAndNothingElse()
    {
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "nothing");

        Assert.Equal("nothing:", Generate(pair));
    }

    [Fact]
    public void AFlowMappingIsItsBracesAroundCommaSeparatedPairs()
    {
        var model = NewModel();
        var mapping = model.NewNode(YamlConceptKeys.FlowMapping);
        FlowPair(model, mapping, "a", "1");
        FlowPair(model, mapping, "b", "2");

        Assert.Equal("{ a: 1, b: 2 }", Generate(mapping));
    }

    [Fact]
    public void AnEmptyFlowCollectionIsItsTwoDelimiters()
    {
        // The fastest way to see that a drawing is not a file: the drawing shows the empty slot, the file does not.
        var model = NewModel();

        Assert.Equal("{}", Generate(model.NewNode(YamlConceptKeys.FlowMapping)));
        Assert.Equal("[]", Generate(model.NewNode(YamlConceptKeys.FlowSequence)));
    }

    [Fact]
    public void AFlowSequenceIsItsBracketsAroundCommaSeparatedValues()
    {
        var model = NewModel();
        var sequence = model.NewNode(YamlConceptKeys.FlowSequence);

        foreach (var text in new[] { "1", "2" })
        {
            model.NewChild(sequence, YamlLanguage.ItemsSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, text);
        }

        Assert.Equal("[ 1, 2 ]", Generate(sequence));
    }

    [Fact]
    public void AScalarIsEmittedVerbatimSoTheSpellingSurvives()
    {
        // No part escapes and no part quotes: the slot holds the file's own spelling, which is what makes the round trip
        // exact without a second answer to "how is this value written".
        var model = NewModel();

        foreach (var spelling in new[] { "\"quoted\"", "'single'", "plain", "|\n  one", "&base 1" })
        {
            var scalar = model.NewNode(YamlConceptKeys.Scalar);
            scalar.Set(YamlLanguage.TextSlot, spelling);

            Assert.Equal(spelling, Generate(scalar));
        }
    }

    [Fact]
    public void AnAliasDrawsItsStarAndACloseNoteItsHash()
    {
        var model = NewModel();

        var alias = model.NewNode(YamlConceptKeys.Alias);
        alias.Set(YamlLanguage.NameSlot, "base");

        var note = model.NewNode(YamlConceptKeys.Comment);
        note.Set(YamlLanguage.TextSlot, " a note");

        Assert.Equal("*base", Generate(alias));
        Assert.Equal("# a note", Generate(note));
    }

    [Fact]
    public void ADocumentWritesItsEntriesOnePerLine()
    {
        var model = NewModel();
        var document = model.NewNode(YamlConceptKeys.Document);

        var marker = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Raw);
        marker.Set(YamlLanguage.TextSlot, "---");

        var note = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Comment);
        note.Set(YamlLanguage.TextSlot, " a note");

        var mapping = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Mapping);
        Pair(model, mapping, "a", "1");

        Assert.Equal("---\n# a note\na: 1", Generate(document));
    }

    [Fact]
    public void TheGeneratorIsNamedSoAMenuCanListIt()
    {
        Assert.Equal("YAML", YamlTextGenerators.Yaml.Name);
        Assert.Same(YamlTextGenerators.Yaml, Assert.Single(YamlTextGenerators.All));
    }

    /// <summary>A pair of a block mapping, with a scalar key and a scalar value.</summary>
    private static void Pair(Model model, Node mapping, string key, string value)
    {
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, key);
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, value);
        pair.Set(YamlLanguage.LayoutSlot, YamlLanguage.Inline);
    }

    private static void FlowPair(Model model, Node mapping, string key, string value)
    {
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.FlowPair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, key);
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, value);
    }

    private static string Generate(Node node)
    {
        var generated = new Generator().Generate(node, YamlTextGenerators.Yaml);

        Assert.True(generated.Succeeded, generated.ToString());
        return generated.Text;
    }

    private static Model NewModel() => YamlLanguage.NewProject().NewModel("pipeline");
}
