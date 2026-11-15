using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Checks the decoration the sample language declares for its own model: a rule that rejected its
/// request is struck through, and one that did not is left alone.
/// </summary>
/// <remarks>
/// This is the case the feature exists for. The strike is not in the notation, because a rule does not
/// always read struck through - only this particular rule does, and only for as long as its outcome
/// says so.
/// </remarks>
public sealed class RejectedRuleEmphasisTests
{
    [Theory]
    [InlineData(RuleOutcomes.Reject, TextEmphasis.Strikethrough)]
    [InlineData(RuleOutcomes.Accept, TextEmphasis.None)]
    [InlineData(RuleOutcomes.Review, TextEmphasis.None)]
    public void OnlyARejectedRuleIsStruckThrough(string outcome, TextEmphasis expected)
    {
        var rule = Rule();

        rule.Set("outcome", outcome);

        Assert.Equal(expected, RejectedRuleEmphasis.Instance.EmphasisOf(rule));
    }

    [Fact]
    public void TheRulesetItselfIsNotDecorated()
    {
        var model = PayrollSample.NewSampleModel();

        Assert.Equal(TextEmphasis.None, RejectedRuleEmphasis.Instance.EmphasisOf(model.Roots[0]));
    }

    [Fact]
    public void ARejectedRuleIsDrawnStruckThroughWhileItsNeighbourIsNot()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];
        var rejected = model.NewChild(ruleset, "rules", RulesConceptKeys.Rule);
        rejected.Set("name", "Dropped");
        rejected.Set("outcome", RuleOutcomes.Reject);

        var tree = new EditorTreeBuilder(
            RulesLanguage.CreateNotations(model.Concepts),
            RulesLanguage.Emphasis).Build(ruleset);

        Assert.Equal(TextEmphasis.Strikethrough, EmphasisOf(tree, "Dropped"));
        Assert.Equal(TextEmphasis.None, EmphasisOf(tree, "Approved"));
    }

    [Fact]
    public void TheStrikeReachesTheKeywordThatIsAlreadyEmphasisedByTheNotation()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = model.Roots[0].GetChildren("rules")[0];
        rule.Set("outcome", RuleOutcomes.Reject);

        var tree = new EditorTreeBuilder(
            RulesLanguage.CreateNotations(model.Concepts),
            RulesLanguage.Emphasis).Build(rule);

        Assert.Equal(TextEmphasis.Bold | TextEmphasis.Strikethrough, EmphasisOf(tree, "rule"));
        Assert.Equal(TextEmphasis.Italic | TextEmphasis.Strikethrough, EmphasisOf(tree, "when"));
    }

    private static Node Rule()
    {
        var model = PayrollSample.NewSampleModel();

        return model.Roots[0].GetChildren("rules")[0];
    }

    private static TextEmphasis EmphasisOf(EditorTree tree, string text) =>
        tree.Cells().OfType<TextCell>().Single(cell => cell.Text == text).Emphasis;
}
