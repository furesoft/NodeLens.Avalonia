using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Checks where the emphasis on a text cell comes from: the notation, the model-driven resolver, and
/// the fact that a decoration made for a node applies to everything drawn inside it.
/// </summary>
public sealed class EditorTreeEmphasisTests
{
    [Fact]
    public void ANotationThatDeclaresNoEmphasisDrawsEveryTextCellRegular()
    {
        var tree = Build(TestNotations.Registry);

        Assert.All(tree.Cells().OfType<TextCell>(), cell => Assert.Equal(TextEmphasis.None, cell.Emphasis));
    }

    [Fact]
    public void EmphasisDeclaredByTheNotationReachesTheTextCell()
    {
        var tree = Build(EmphasisNotations.Registry);

        Assert.Equal(TextEmphasis.Bold, EmphasisOf(tree, "rule"));
        Assert.Equal(TextEmphasis.Italic, EmphasisOf(tree, "walk"));
        Assert.Equal(TextEmphasis.None, EmphasisOf(tree, "unnamed"));
    }

    [Fact]
    public void EmphasisTheResolverAsksForCoversEverythingDrawnInsideThatNode()
    {
        var tree = Build(
            EmphasisNotations.Registry,
            ConceptEmphasis.On(TestLanguage.Body, TextEmphasis.Strikethrough));

        Assert.Equal(TextEmphasis.Strikethrough | TextEmphasis.Italic, EmphasisOf(tree, "walk"));
        Assert.Equal(TextEmphasis.Bold, EmphasisOf(tree, "rule"));
        Assert.Equal(TextEmphasis.None, EmphasisOf(tree, "unnamed"));
    }

    [Fact]
    public void EmphasisFromTheNotationAndFromTheResolverAddUp()
    {
        var tree = Build(
            EmphasisNotations.Registry,
            ConceptEmphasis.On(TestLanguage.Rule, TextEmphasis.Underline));

        Assert.Equal(TextEmphasis.Bold | TextEmphasis.Underline, EmphasisOf(tree, "rule"));
        Assert.Equal(TextEmphasis.Underline, EmphasisOf(tree, "unnamed"));
        Assert.Equal(TextEmphasis.Underline | TextEmphasis.Italic, EmphasisOf(tree, "walk"));
    }

    [Fact]
    public void ASessionDecoratesTheDocumentWithTheResolverItIsGiven()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        step.Set("text", "walk");

        using var session = new EditorSession(
            rule,
            TestNotations.Registry,
            emphasis: ConceptEmphasis.On(TestLanguage.Step, TextEmphasis.Underline));

        Assert.Equal(TextEmphasis.Underline, EmphasisOf(session.Tree, "walk"));
        Assert.Equal(TextEmphasis.None, EmphasisOf(session.Tree, "unnamed"));
    }

    private static EditorTree Build(NotationRegistry notations, ICellEmphasis? emphasis = null)
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        step.Set("text", "walk");

        return new EditorTreeBuilder(notations, emphasis).Build(rule);
    }

    private static TextEmphasis EmphasisOf(EditorTree tree, string text) =>
        tree.Cells().OfType<TextCell>().Single(cell => cell.Text == text).Emphasis;
}
