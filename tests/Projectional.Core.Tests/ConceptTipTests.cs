using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The concept tooltip is decided without a window: what it says comes from the language, and how it is
/// broken comes from the measure.
/// </summary>
public sealed class ConceptTipTests
{
    private const string Sentence = "A named rule with a body and an outcome.";

    [Fact]
    public void AConceptWithADescriptionHasATip()
    {
        var rule = TestEditor.BareRule();

        var content = ConceptTip.Of(rule);

        Assert.NotNull(content);
        Assert.Equal("Test.Rule", content.Heading);
        Assert.Equal(Sentence, content.Description);
    }

    [Fact]
    public void AConceptWithoutADescriptionHasNoTip()
    {
        // An empty box that repeats the concept key would teach the user that hovering is pointless.
        Assert.Null(ConceptTip.Of(TestEditor.BareStep()));
    }

    [Fact]
    public void TheKeyComesFirstAndTheDescriptionFollows()
    {
        var rule = TestEditor.BareRule();

        var lines = TipLines.Of(ConceptTip.Of(rule)!, 80, MonospaceTextMeasure.Instance);

        Assert.Equal("Test.Rule", lines[0]);
        Assert.Equal(Sentence, string.Join(' ', lines.Skip(1)));
    }

    [Fact]
    public void ADescriptionIsBrokenToFitTheRoomItIsGiven()
    {
        var rule = TestEditor.BareRule();

        var lines = TipLines.Of(ConceptTip.Of(rule)!, 12, MonospaceTextMeasure.Instance);

        Assert.True(lines.Count > 2, "a sentence of 40 characters cannot fit in 12 units on two lines");

        foreach (var line in lines)
        {
            Assert.True(
                MonospaceTextMeasure.Instance.Measure(line, TextEmphasis.None) <= 12,
                $"'{line}' is wider than the box");
        }
    }

    [Fact]
    public void AKeyLongerThanTheBoxIsBrokenRatherThanOverflowing()
    {
        // The box is placed from the width the measure reports, so an overflowing line would make it a lie.
        var content = new TipContent("Rules.Very.Long.Key", "x");

        var lines = TipLines.Of(content, 8, MonospaceTextMeasure.Instance);

        foreach (var line in lines)
        {
            Assert.True(MonospaceTextMeasure.Instance.Measure(line, TextEmphasis.None) <= 8);
        }
    }

    [Fact]
    public void ASessionAnswersForTheNodeAnIdNames()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        Assert.Equal(Sentence, ConceptTip.Of(session, rule.Id)?.Description);
    }

    [Fact]
    public void AnIdThatNoLongerExistsHasNoTip()
    {
        // A pointer can outlive the node it was over, so the lookup answers "nothing to show" rather than
        // throwing at the moment the user moves the mouse.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        Assert.Null(ConceptTip.Of(session, NodeId.New()));
    }

    [Fact]
    public void TheArgumentsAreRequired()
    {
        Assert.Throws<ArgumentNullException>(() => ConceptTip.Of(null!));
        Assert.Throws<ArgumentNullException>(
            () => TipLines.Of(null!, 80, MonospaceTextMeasure.Instance));
        Assert.Throws<ArgumentNullException>(
            () => TipLines.Of(new TipContent("Test.Rule", Sentence), 80, null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TipLines.Of(new TipContent("Test.Rule", Sentence), 0, MonospaceTextMeasure.Instance));
        Assert.Throws<ArgumentException>(() => new TipContent("", Sentence));
        Assert.Throws<ArgumentException>(() => new TipContent("Test.Rule", ""));
    }
}
