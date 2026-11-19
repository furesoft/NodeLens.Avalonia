using Projectional.Core;
using Projectional.Projection;
using Projectional.Yaml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>How a YAML document is drawn: one line per member, one dash per item, braces for a flow collection.</summary>
/// <remarks>
/// The two collection kinds are separate concepts here, so the drawing says which one the model holds. A pair draws its
/// value inline even when the file writes it on the next line - a drawing is not a file, and the layout engine breaks the
/// line by itself when the child's notation is vertical.
/// </remarks>
public class YamlNotationTests
{
    [Fact]
    public void AMappingDrawsOnePairPerLine()
    {
        var model = NewModel();
        var mapping = model.NewNode(YamlConceptKeys.Mapping);

        Pair(model, mapping, "host", "localhost");
        Pair(model, mapping, "port", "8080");

        Assert.Equal("host: localhost\nport: 8080", Render(mapping));
    }

    [Fact]
    public void ASequenceDrawsADashPerItem()
    {
        var model = NewModel();
        var sequence = model.NewNode(YamlConceptKeys.Sequence);

        Item(model, sequence, "one");
        Item(model, sequence, "two");

        Assert.Equal("- one\n- two", Render(sequence));
    }

    [Fact]
    public void APairWithoutAValueDrawsItsColonAndNothingElse()
    {
        // `key:` is a null value in YAML, and the drawing must not invent a blank that a user would have to delete.
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "nothing");

        Assert.Equal("nothing:", Render(pair));
    }

    [Fact]
    public void AnEmptyMappingAndAnEmptySequenceKeepTheirSlotVisible()
    {
        // A hidden slot would have no caret stop, and this language offers no intention that could fill it.
        var model = NewModel();

        Assert.Equal("empty", Render(model.NewNode(YamlConceptKeys.Mapping)));
        Assert.Equal("empty", Render(model.NewNode(YamlConceptKeys.Sequence)));
        Assert.Equal("empty", Render(model.NewNode(YamlConceptKeys.Document)));
    }

    [Fact]
    public void AFlowMappingDrawsItsBraces()
    {
        var model = NewModel();
        var mapping = model.NewNode(YamlConceptKeys.FlowMapping);
        FlowPair(model, mapping, "a", "1");

        Assert.Equal("{ a: 1 }", Render(mapping));
    }

    [Fact]
    public void AnEmptyFlowMappingIsTwoBraces()
    {
        // An empty list still draws its placeholder inside the delimiters, because an empty slot has to stay a caret stop;
        // the file prints `{}`, and a drawing is not the file.
        var model = NewModel();

        Assert.Equal("{ }", Render(model.NewNode(YamlConceptKeys.FlowMapping)));
        Assert.Equal("[ ]", Render(model.NewNode(YamlConceptKeys.FlowSequence)));
    }

    [Fact]
    public void AFlowSequenceSeparatesItsItemsWithAComma()
    {
        var model = NewModel();
        var sequence = model.NewNode(YamlConceptKeys.FlowSequence);

        foreach (var text in new[] { "1", "2" })
        {
            model.NewChild(sequence, YamlLanguage.ItemsSlot, YamlConceptKeys.Scalar)
                .Set(YamlLanguage.TextSlot, text);
        }

        Assert.Equal("[ 1, 2 ]", Render(sequence));
    }

    [Fact]
    public void AScalarDrawsTheSpellingItHolds()
    {
        var model = NewModel();
        var scalar = model.NewNode(YamlConceptKeys.Scalar);
        scalar.Set(YamlLanguage.TextSlot, "\"quoted\"");

        Assert.Equal("\"quoted\"", Render(scalar));
    }

    [Fact]
    public void AnAliasACloseNoteAndARawLineDrawThemselves()
    {
        var model = NewModel();

        var alias = model.NewNode(YamlConceptKeys.Alias);
        alias.Set(YamlLanguage.NameSlot, "base");

        var note = model.NewNode(YamlConceptKeys.Comment);
        note.Set(YamlLanguage.TextSlot, " a note");

        var raw = model.NewNode(YamlConceptKeys.Raw);
        raw.Set(YamlLanguage.TextSlot, "---");

        Assert.Equal("*base", Render(alias));
        Assert.Equal("# a note", Render(note));
        Assert.Equal("---", Render(raw));
    }

    [Fact]
    public void AMappingInsideAPairValueHangsFromWhereItsCellStarts()
    {
        // The child's notation is vertical, so the drawing stacks it - measured from the cell the block begins at, which is
        // the documented consequence of drawing a vertical notation inside an inline child (HOCON's object has the same
        // one). The file that is generated is indented from the line start instead, because a template's indent is a
        // property of the template.
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "server");

        var value = model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Mapping);
        Pair(model, value, "port", "8080");
        Pair(model, value, "host", "localhost");

        Assert.Equal("server: port: 8080\n        host: localhost", Render(pair));
    }

    private static void Pair(Model model, Node mapping, string key, string value)
    {
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, key);
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, value);
    }

    private static void FlowPair(Model model, Node mapping, string key, string value)
    {
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.FlowPair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, key);
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, value);
    }

    private static void Item(Model model, Node sequence, string text)
    {
        var item = model.NewChild(sequence, YamlLanguage.ItemsSlot, YamlConceptKeys.SequenceItem);
        model.NewChild(item, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, text);
    }

    private static string Render(Node node) =>
        LayoutSnapshot.Render(node, YamlLanguage.Notations, width: 120).Trim();

    private static Model NewModel() => YamlLanguage.NewProject().NewModel("pipeline");
}
