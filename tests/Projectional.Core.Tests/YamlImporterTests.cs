using Projectional.Core;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Yaml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>YAML in: the document the file describes, held in the text the file was written in.</summary>
/// <remarks>
/// YAML's layout is part of its syntax, so this language keeps facts the other reference languages do not: which
/// collection kind a node is, whether a pair's value stands on the key's line, and whether a construct was modelled at all
/// or kept as raw text. The assertions are therefore about the spelling as much as about the structure.
/// </remarks>
public sealed class YamlImporterTests
{
    [Fact]
    public void TheFileIsOneDocumentRoot()
    {
        // A host projects one root, and a YAML file is a stream: the note above the mapping and the mapping itself are two
        // entries of one document.
        var (_, result) = Import("# a note\na: 1\n");

        var document = Assert.Single(result.Roots);

        Assert.Equal(YamlConceptKeys.Document, document.Concept.Key);
        Assert.Equal(
            [YamlConceptKeys.Comment, YamlConceptKeys.Mapping],
            document.GetChildren(YamlLanguage.EntriesSlot).Select(entry => entry.Concept.Key));
    }

    [Fact]
    public void AMappingKeepsTheOrderOfItsPairs()
    {
        var (_, result) = Import("b: 1\na: 2\n");

        Assert.Equal(["b", "a"], Keys(MappingOf(result)));
    }

    [Fact]
    public void ARepeatedKeyIsKeptTwice()
    {
        // YAML permits it as much as HOCON does, and a model that dropped one would rewrite the file.
        var (_, result) = Import("a: 1\na: 2\n");

        Assert.Equal(["a", "a"], Keys(MappingOf(result)));
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("a: 1", "1")]
    [InlineData("a: hello world", "hello world")]
    [InlineData("a: \"quoted\"", "\"quoted\"")]
    [InlineData("a: 'single'", "'single'")]
    [InlineData("a: 'it''s'", "'it''s'")]
    public void AScalarKeepsTheSliceOfTheFileItCameFrom(string source, string expected)
    {
        var value = Value(source + "\n");

        Assert.Equal(YamlConceptKeys.Scalar, value.Concept.Key);
        Assert.Equal(expected, value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void ABlockScalarKeepsItsIndicatorAndItsBody()
    {
        // The body's own indentation is part of the text, so the spelling has to carry it or a round trip would reflow it.
        // The break that ends the last line does not: that one belongs to the line structure, which the separators write.
        var value = Value("a: |\n  one\n  two\n");

        Assert.Equal("|\n  one\n  two", value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void AFoldedScalarKeepsItsChompingIndicator()
    {
        var value = Value("a: >-\n  one\n  two\n");

        Assert.Equal(">-\n  one\n  two", value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void AnAnchoredScalarKeepsTheAnchorInItsSpelling()
    {
        // An anchor in front of a scalar is one run of text with it, so the scalar holds both and needs no second slot.
        var value = Value("a: &base 1\n");

        Assert.Equal(YamlConceptKeys.Scalar, value.Concept.Key);
        Assert.Equal("&base 1", value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void ATaggedScalarKeepsTheTagInItsSpelling()
    {
        var value = Value("a: !!str 1\n");

        Assert.Equal("!!str 1", value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void AnAliasNamesItsAnchorAndIsNeverFollowed()
    {
        var value = Value("a: *base\n");

        Assert.Equal(YamlConceptKeys.Alias, value.Concept.Key);
        Assert.Equal("base", value.GetOrNull(YamlLanguage.NameSlot));
    }

    [Fact]
    public void ABranchOnTheKeyLineIsInline()
    {
        var pair = PairOf("a: 1\n");

        Assert.Equal(YamlLanguage.Inline, pair.GetOrNull(YamlLanguage.LayoutSlot));
    }

    [Fact]
    public void ABlockMappingBelowItsKeyIsNested()
    {
        var pair = PairOf("a:\n  b: 1\n");

        Assert.Equal(YamlLanguage.Nested, pair.GetOrNull(YamlLanguage.LayoutSlot));
        Assert.Equal(YamlConceptKeys.Mapping, pair.GetChild(YamlLanguage.ValueSlot)!.Concept.Key);
    }

    [Fact]
    public void ABlockSequenceBelowItsKeyIsNested()
    {
        var pair = PairOf("a:\n  - 1\n");

        Assert.Equal(YamlLanguage.Nested, pair.GetOrNull(YamlLanguage.LayoutSlot));
        Assert.Equal(YamlConceptKeys.Sequence, pair.GetChild(YamlLanguage.ValueSlot)!.Concept.Key);
    }

    [Fact]
    public void ASequenceKeepsOneItemPerDash()
    {
        var value = Value("a:\n  - one\n  - two\n");

        var items = value.GetChildren(YamlLanguage.ItemsSlot);

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(YamlConceptKeys.SequenceItem, item.Concept.Key));
        Assert.Equal(
            ["one", "two"],
            items.Select(item => item.GetChild(YamlLanguage.ValueSlot)!.GetOrNull(YamlLanguage.TextSlot)));
    }

    [Fact]
    public void ASequenceInsideASequenceIsItsOwnItem()
    {
        var value = Value("a:\n  - - one\n    - two\n");

        var inner = Assert.Single(Value("a:\n  - - one\n    - two\n").GetChildren(YamlLanguage.ItemsSlot)[0]
            .GetChildren(YamlLanguage.ValueSlot));

        Assert.Equal(YamlConceptKeys.Sequence, inner.Concept.Key);
        Assert.Equal(2, inner.GetChildren(YamlLanguage.ItemsSlot).Count);
    }

    [Fact]
    public void ACompactMappingInsideAnItemIsAMapping()
    {
        var model = ModelOf("a:\n  - b: 1\n");

        var item = Assert.Single(
            PairOf(Assert.Single(model.Roots), "a")
                .GetChild(YamlLanguage.ValueSlot)!
                .GetChildren(YamlLanguage.ItemsSlot));

        Assert.Equal(YamlConceptKeys.Mapping, item.GetChild(YamlLanguage.ValueSlot)!.Concept.Key);
    }

    [Fact]
    public void AFlowMappingIsItsOwnConcept()
    {
        // A flow collection and a block collection are different concepts rather than one concept with a style, because a
        // notation is chosen per concept - a style slot could not have given one node two drawings.
        var value = Value("a: { b: 1, c: 2 }\n");

        Assert.Equal(YamlConceptKeys.FlowMapping, value.Concept.Key);
        Assert.Equal(2, value.GetChildren(YamlLanguage.MembersSlot).Count);
        Assert.All(
            value.GetChildren(YamlLanguage.MembersSlot),
            pair => Assert.Equal(YamlConceptKeys.FlowPair, pair.Concept.Key));
    }

    [Fact]
    public void AFlowSequenceHoldsFlowValues()
    {
        var value = Value("a: [ 1, { b: 2 }, [ 3 ] ]\n");

        Assert.Equal(YamlConceptKeys.FlowSequence, value.Concept.Key);
        Assert.Equal(
            [YamlConceptKeys.Scalar, YamlConceptKeys.FlowMapping, YamlConceptKeys.FlowSequence],
            value.GetChildren(YamlLanguage.ItemsSlot).Select(item => item.Concept.Key));
    }

    [Fact]
    public void AFlowSequenceNeedsItsCommas()
    {
        // YAML separates flow entries with a comma, so `[ 1 2 ]` is one scalar that happens to hold a space - which is the
        // reason the importer never guesses where an item ends.
        var value = Value("a: [ 1 2 ]\n");

        Assert.Equal(YamlConceptKeys.Scalar, Assert.Single(value.GetChildren(YamlLanguage.ItemsSlot)).Concept.Key);
    }

    [Fact]
    public void ANoteAboveAMappingIsAnEntryOfTheDocument()
    {
        var (_, result) = Import("# first\n# second\na: 1\n");

        var entries = Assert.Single(result.Roots).GetChildren(YamlLanguage.EntriesSlot);

        Assert.Equal(
            [" first", " second"],
            entries.Take(2).Select(entry => entry.GetOrNull(YamlLanguage.TextSlot)));
    }

    [Fact]
    public void ANoteInsideAMappingIsOneOfItsMembers()
    {
        var (_, result) = Import("a: 1\n# between\nb: 2\n");

        var members = MappingOf(result).GetChildren(YamlLanguage.MembersSlot);

        Assert.Equal(
            [YamlConceptKeys.Pair, YamlConceptKeys.Comment, YamlConceptKeys.Pair],
            members.Select(member => member.Concept.Key));
    }

    [Fact]
    public void ANoteBeforeANestedMappingStaysInsideIt()
    {
        // The note is read before the collection it introduces exists, so it is held until a list starts - otherwise it
        // would be moved out to the list that asked for the value.
        var (_, result) = Import("server:\n  # inner\n  port: 8080\n");

        var server = PairOf(result, "server").GetChild(YamlLanguage.ValueSlot)!;

        Assert.Equal(YamlConceptKeys.Comment, server.GetChildren(YamlLanguage.MembersSlot)[0].Concept.Key);
    }

    [Fact]
    public void ANoteBesideAValueIsReportedBecauseItMoves()
    {
        var (_, result) = Import("a: 1  # beside\n");

        var warning = Assert.Single(result.Diagnostics);

        Assert.Equal(YamlImporter.CommentMoved, warning.Code);
        Assert.Equal(ImportSeverity.Warning, warning.Severity);
        Assert.Equal(1, warning.Line);
    }

    [Fact]
    public void ANoteInsideAFlowCollectionIsHoistedAndReported()
    {
        // A flow collection has no member list a note could join, so it moves out to the surrounding list rather than being
        // dropped - and the move is said out loud.
        var (_, result) = Import("a: { b: 1, # inside\n  c: 2 }\n");

        Assert.Equal(YamlImporter.CommentMoved, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(
            [YamlConceptKeys.Pair, YamlConceptKeys.Comment],
            MappingOf(result).GetChildren(YamlLanguage.MembersSlot).Select(member => member.Concept.Key));

        // The flow mapping itself keeps both of its members; only the note left it.
        Assert.Equal(2, PairOf(result).GetChild(YamlLanguage.ValueSlot)!.GetChildren(YamlLanguage.MembersSlot).Count);
    }

    [Fact]
    public void ADocumentMarkerIsKeptAsRawText()
    {
        var (_, result) = Import("---\na: 1\n");

        var document = Assert.Single(result.Roots);
        var marker = document.GetChildren(YamlLanguage.EntriesSlot)[0];

        Assert.Equal(YamlConceptKeys.Raw, marker.Concept.Key);
        Assert.Equal("---", marker.GetOrNull(YamlLanguage.TextSlot));

        // Nothing was lost, so nothing is reported.
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ADirectiveIsKeptAsRawTextAndReported()
    {
        var (_, result) = Import("%YAML 1.2\n---\na: 1\n");

        var raw = Assert.Single(result.Roots).GetChildren(YamlLanguage.EntriesSlot)[0];

        Assert.Equal(YamlConceptKeys.Raw, raw.Concept.Key);
        Assert.Equal("%YAML 1.2", raw.GetOrNull(YamlLanguage.TextSlot));
        Assert.Equal(YamlImporter.KeptAsRaw, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void AnAnchoredCollectionIsKeptAsRawTextAndReported()
    {
        // A template cannot ask what kind of node a child is, so an anchored collection has no slot for its prefix. Keeping
        // the whole node verbatim is the honest answer: the file survives and the construct is not pretended to be modelled.
        var value = Value("a: &base\n  b: 1\n");

        Assert.Equal(YamlConceptKeys.Raw, value.Concept.Key);
        Assert.Equal("&base\n  b: 1", value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void AComplexKeyIsKeptAsOneRawLine()
    {
        var (_, result) = Import("? [a, b]\n: value\n");

        var raw = Assert.Single(MappingOf(result).GetChildren(YamlLanguage.MembersSlot));

        Assert.Equal(YamlConceptKeys.Raw, raw.Concept.Key);
        Assert.Equal("? [a, b]\n: value", raw.GetOrNull(YamlLanguage.TextSlot));
        Assert.Equal(YamlImporter.KeptAsRaw, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ABlockScalarBodyIsReadAsTheFileWroteIt()
    {
        var value = Value("a: |\n  one\n    two\n");

        Assert.Equal("|\n  one\n    two", value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void TextThatIsNotYamlImportsNothing()
    {
        var model = YamlLanguage.NewProject().NewModel("pipeline");
        var before = ModelXml.Serialize(model);
        var version = model.Version;

        var result = YamlLanguage.Importer.Import(model, "- a\nb: [ 1\n");

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
    public void ARefusalNamesThePlaceTheTokensStoppedMakingSense()
    {
        // A flow sequence holding a key has no reading in this language, and the refusal names the token rather than the
        // top of the file - that is the difference the syntax exception exists for.
        var (_, result) = Import("a: [ b: 1 ]\n");

        var error = Assert.Single(result.Diagnostics);

        Assert.Equal(YamlImporter.UnexpectedToken, error.Code);
        Assert.Equal(1, error.Line);
        Assert.Equal(6, error.Column);
    }

    [Fact]
    public void AnImportAppendsToANonEmptyModel()
    {
        var model = YamlLanguage.NewProject().NewModel("pipeline");
        var existing = model.NewNode(YamlConceptKeys.Document);

        var result = YamlLanguage.Importer.Import(model, "a: 1\n");

        Assert.True(result.Succeeded);
        Assert.Equal(2, model.Roots.Count);
        Assert.Same(existing, model.Roots[0]);
    }

    [Fact]
    public void AnImportIsOneUndoStep()
    {
        var model = YamlLanguage.NewProject().NewModel("pipeline");
        using var history = new UndoHistory(model);

        YamlLanguage.Importer.Import(model, "a: 1\nb:\n  - 1\n  - 2\n");

        Assert.Equal(1, history.UndoDepth);

        Assert.True(history.Undo());
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void WindowsLineBreaksAreNormalizedBeforeTheTokensAreRead()
    {
        // A block scalar's body is a slice of the text, so a carriage return would travel into the model and out into a
        // file that no longer matches.
        var (_, result) = Import("a: |\r\n  one\r\n");
        var value = PairOf(result, "a").GetChild(YamlLanguage.ValueSlot)!;

        Assert.Equal("|\n  one", value.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void TheFormatNameIsWhatAFileDialogFiltersOn()
    {
        Assert.Equal("yaml", YamlLanguage.Importer.Format);

        Assert.True(YamlLanguage.Importer.CanImport("a: 1"));
        Assert.True(YamlLanguage.Importer.CanImport("---\na: 1"));
        Assert.True(YamlLanguage.Importer.CanImport("- one"));
        Assert.True(YamlLanguage.Importer.CanImport("{ a: 1 }"));

        // A pair of TOML or HOCON is no pair of YAML's: the colon has to come before any equals sign.
        Assert.False(YamlLanguage.Importer.CanImport("a = 1"));
        Assert.False(YamlLanguage.Importer.CanImport(""));
        Assert.False(YamlLanguage.Importer.CanImport("# only a note"));
        Assert.False(YamlLanguage.Importer.CanImport("<a/>"));
    }

    /// <summary>The value of a one-pair document, as the importer built it.</summary>
    private static Node Value(string source) => PairOf(Import(source).Result).GetChild(YamlLanguage.ValueSlot)!;

    /// <summary>The one pair of a document whose mapping holds a single line.</summary>
    private static Node PairOf(string source) => PairOf(Import(source).Result);

    private static Node PairOf(ImportResult result) => PairOf(result, "a");

    private static Node PairOf(ImportResult result, string key) => PairOf(DocumentOf(result), key);

    private static Node PairOf(Node document, string key)
    {
        // A mapping's members may hold notes as well as pairs, so the pairs are separated before the key is asked for.
        var pairs = MappingOf(document).GetChildren(YamlLanguage.MembersSlot)
            .Where(member => member.Concept.Key == YamlConceptKeys.Pair)
            .ToList();

        return Assert.Single(
            pairs,
            pair => pair.GetChild(YamlLanguage.KeySlot)?.GetOrNull(YamlLanguage.TextSlot) == key);
    }

    /// <summary>The mapping a one-entry document holds.</summary>
    private static Node MappingOf(ImportResult result) => MappingOf(DocumentOf(result));

    private static Node MappingOf(Node document) =>
        Assert.Single(document.GetChildren(YamlLanguage.EntriesSlot));

    private static Node DocumentOf(ImportResult result) => Assert.Single(result.Roots);

    /// <summary>The keys of a mapping's pairs, in the order the file had them.</summary>
    private static IEnumerable<string?> Keys(Node mapping) =>
        mapping.GetChildren(YamlLanguage.MembersSlot)
            .Where(member => member.Concept.Key == YamlConceptKeys.Pair)
            .Select(member => member.GetChild(YamlLanguage.KeySlot)?.GetOrNull(YamlLanguage.TextSlot));

    private static Model ModelOf(string yaml) => Import(yaml).Model;

    private static (Model Model, ImportResult Result) Import(string yaml)
    {
        var model = YamlLanguage.NewProject().NewModel("pipeline");
        return (model, YamlLanguage.Importer.Import(model, yaml));
    }
}
