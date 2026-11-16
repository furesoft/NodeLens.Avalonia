using Projectional.Core;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A folded node is one cell with the node's own address, which is why layout, caret, snapshot and hit test
/// all handle it without a folding case.
/// </summary>
public sealed class CollapsedCellTests
{
    [Fact]
    public void AFoldedNodeIsOneLineOfSummary()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        Named(rule, "walk");

        var snapshot = Draw(rule, Fold(rule));

        Assert.Contains("walk", snapshot, StringComparison.Ordinal);
        Assert.Contains(CollapsedSummary.Omission, snapshot, StringComparison.Ordinal);

        // The body's own content is gone: the whole subtree is replaced, not just the steps.
        Assert.DoesNotContain(body.Id.ToString(), snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void AFoldedOuterNodeHidesAFoldedInnerOne()
    {
        var (rule, body, step) = TestEditor.RuleWithStep();

        var snapshot = Draw(rule, Fold(rule, body, step));

        // One line, and no trace of the inner nodes: unfolding the outer one brings them back still folded,
        // because their ids never left the set.
        Assert.Single(snapshot.Split('\n'));
        Assert.DoesNotContain(CollapsedSummary.Omission + CollapsedSummary.Omission, snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeWithoutPermissionIsDrawnAsIfItWereNotInTheSet()
    {
        // Test.Step nominates no collapse slot, so putting it in the set changes nothing at all.
        var (rule, body, step) = TestEditor.RuleWithStep();

        var untouched = Draw(rule, Fold(body, step));
        var plain = Draw(rule, []);

        Assert.Equal(plain, untouched);
    }

    [Fact]
    public void TheSummaryIsTheNameWhenThereIsOneAndTheConceptKeyOtherwise()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        Named(rule, "walk");

        Assert.Equal($"walk {CollapsedSummary.Omission}", CollapsedSummary.Of(rule));

        var anonymous = TestEditor.BareStep();
        Assert.Equal($"Test.Step {CollapsedSummary.Omission}", CollapsedSummary.Of(anonymous));
    }

    private static void Named(Node node, string name)
    {
        using (node.Model.BeginMutation())
        {
            node.Set("name", name);
        }
    }

    private static HashSet<NodeId> Fold(params Node[] nodes) => [.. nodes.Select(node => node.Id)];

    private static string Draw(Node root, HashSet<NodeId> collapsed)
    {
        var tree = new EditorTreeBuilder(TestNotations.Registry, collapsed: collapsed).Build(root);
        var layout = new CellLayouter(MonospaceTextMeasure.Instance).Layout(tree);

        return LayoutSnapshot.Render(layout, width: 60);
    }
}
