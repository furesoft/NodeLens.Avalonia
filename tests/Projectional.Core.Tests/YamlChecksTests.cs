using Projectional.Core;
using Projectional.Editing;
using Projectional.Yaml;
using Projectional.Yaml.Checks;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The rules YAML states about itself, and the repair each finding carries.</summary>
/// <remarks>
/// Both rules are about a value that would no longer read back: a block collection on a key's line, and a scalar whose
/// spelling stopped being one. There is deliberately no duplicate-key rule - YAML permits a repeated key, and a repair
/// that removed one would rewrite the file.
/// </remarks>
public sealed class YamlChecksTests
{
    [Fact]
    public void ABlockCollectionOnItsKeysLineIsReported()
    {
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Mapping);
        pair.Set(YamlLanguage.LayoutSlot, YamlLanguage.Inline);

        var finding = Assert.Single(new YamlPlacementCheck().Find(model));

        Assert.Equal(YamlPlacementCheck.Code, finding.Diagnostic.Code);
        Assert.Equal(YamlLanguage.LayoutSlot, finding.Diagnostic.Role);
    }

    [Fact]
    public void ThePlacementRepairWritesThePlacementTheValueNeeds()
    {
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Sequence);
        pair.Set(YamlLanguage.LayoutSlot, YamlLanguage.Inline);

        var finding = Assert.Single(new YamlPlacementCheck().Find(model));

        using var session = new EditorSession(pair, YamlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal(YamlLanguage.Nested, pair.GetOrNull(YamlLanguage.LayoutSlot));
        Assert.Empty(new YamlPlacementCheck().Find(model));
    }

    [Fact]
    public void AValueAlreadyOnTheNextLineIsLeftAlone()
    {
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Mapping);
        pair.Set(YamlLanguage.LayoutSlot, YamlLanguage.Nested);

        Assert.Empty(new YamlPlacementCheck().Find(model));
    }

    [Fact]
    public void APairBornWithoutAPlacementAndHoldingABlockIsReported()
    {
        // `inline` is the declared default, so a pair a user has just pointed at a block collection is exactly the state the
        // rule exists for - the file would not read back.
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Mapping);

        Assert.Equal(YamlLanguage.Inline, pair.GetOrNull(YamlLanguage.LayoutSlot));
        Assert.Single(new YamlPlacementCheck().Find(model));
    }

    [Fact]
    public void AnInlineScalarIsNoPlacementProblem()
    {
        // The rule is about a block collection, not about the placement property: a scalar belongs on the key's line.
        var model = NewModel();
        var pair = model.NewNode(YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "1");

        Assert.Empty(new YamlPlacementCheck().Find(model));
    }

    [Theory]
    [InlineData("\"quoted\"")]
    [InlineData("|\n  one")]
    [InlineData("he said \"hi\"")]
    public void AScalarThatIsStillOneScalarIsLeftAlone(string spelling)
    {
        // A quote inside a plain scalar is not the start of a quoted one, so the tokenizer - not a rule of this check - is
        // what answers whether a spelling is one scalar.
        var model = NewModel();
        model.NewNode(YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, spelling);

        Assert.Empty(new YamlScalarCheck().Find(model));
    }

    [Theory]
    [InlineData("\"unterminated")]
    [InlineData("a: 1\nb: 2")]
    public void AScalarThatIsNoLongerOneIsReportedAndQuoted(string spelling)
    {
        var model = NewModel();
        var scalar = model.NewNode(YamlConceptKeys.Scalar);
        scalar.Set(YamlLanguage.TextSlot, spelling);

        var finding = Assert.Single(new YamlScalarCheck().Find(model));

        Assert.Equal(YamlScalarCheck.Code, finding.Diagnostic.Code);
        Assert.Equal(YamlLanguage.TextSlot, finding.Diagnostic.Role);

        using var session = new EditorSession(scalar, YamlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal(YamlSpellingQuoted(spelling), scalar.GetOrNull(YamlLanguage.TextSlot));
        Assert.Empty(new YamlScalarCheck().Find(model));
    }

    [Fact]
    public void AQuotedRepairEscapesWhatHasToBeEscaped()
    {
        var model = NewModel();
        var scalar = model.NewNode(YamlConceptKeys.Scalar);

        // A colon and a space make a mapping, so this text is no longer one scalar - and the repair has to escape the quotes
        // it contains on the way in.
        scalar.Set(YamlLanguage.TextSlot, "he said: \"hi\"");

        var finding = Assert.Single(new YamlScalarCheck().Find(model));

        using var session = new EditorSession(scalar, YamlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        session.Apply(finding);

        Assert.Equal("\"he said: \\\"hi\\\"\"", scalar.GetOrNull(YamlLanguage.TextSlot));
    }

    [Fact]
    public void ACleanDocumentIsClean()
    {
        var model = NewModel();
        var document = model.NewNode(YamlConceptKeys.Document);
        var mapping = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Mapping);
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "1");

        Assert.Empty(YamlChecks.All.Find(model));
    }

    /// <summary>The text the repair is expected to write: the value between double quotes, escaped.</summary>
    private static string YamlSpellingQuoted(string text) =>
        "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static Model NewModel() => YamlLanguage.NewProject().NewModel("pipeline");
}
