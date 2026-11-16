using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Folding is view state on the session: it re-projects, the caret survives both directions, and it creates
/// no undo step.
/// </summary>
public sealed class CollapsedSessionTests
{
    [Fact]
    public void FoldingUnderTheCaretLandsOnThePlaceholder()
    {
        var (rule, body, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(CaretPosition.At(CellAddress.OfSlot(step.Id, "text"), 0)));

        session.ToggleCollapsed(rule.Id);

        // The caret stood inside the node that just disappeared, so it falls back to the first place at or
        // after the lost one - which is the placeholder, the node's own address.
        var place = CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret);

        Assert.NotNull(place);
        Assert.IsType<CollapsedCell>(place.Value.Cell);
    }

    [Fact]
    public void UnfoldingLandsInsideTheNode()
    {
        var (rule, body, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        session.ToggleCollapsed(rule.Id);
        session.Select(Selection.At(CaretPosition.At(new CellAddress(rule.Id), 0)));

        session.ToggleCollapsed(rule.Id);

        // A node address reads as "start here", so the caret ends up inside the node again.
        var place = CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret);

        Assert.NotNull(place);
        Assert.IsNotType<CollapsedCell>(place.Value.Cell);
    }

    [Fact]
    public void FoldingIsNotAnEdit()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        var version = rule.Model.Version;
        var undoDepth = session.History.UndoDepth;

        session.ToggleCollapsed(rule.Id);

        Assert.Equal(version, rule.Model.Version);
        Assert.Equal(undoDepth, session.History.UndoDepth);
        Assert.False(session.History.CanUndo);
    }

    [Fact]
    public void TheLayoutShrinks()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        var before = session.Layout.Size.Height;

        session.ToggleCollapsed(rule.Id);

        Assert.True(session.Layout.Size.Height < before, "a folded node is one line");
    }

    [Fact]
    public void AFoldSetThatIsAlreadyInForceDoesNotReProject()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        session.ToggleCollapsed(rule.Id);
        var tree = session.Tree;

        session.Collapsed = session.Collapsed;

        Assert.Same(tree, session.Tree);
    }

    [Fact]
    public void TheSetSurvivesAVariantSwitch()
    {
        // Both view-state values are passed to one builder, so a Choose must not forget the folds.
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        session.ToggleCollapsed(rule.Id);
        session.Choose(null);

        Assert.True(session.Collapsed.Contains(rule.Id));
        Assert.IsType<CollapsedCell>(session.Tree.Find(new CellAddress(rule.Id)));
    }

    [Fact]
    public void AFoldedNodeCannotBeTypedInto()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        session.ToggleCollapsed(rule.Id);
        session.Select(Selection.At(CaretPosition.At(new CellAddress(rule.Id), 0)));

        var version = rule.Model.Version;
        var result = session.Apply(new TypeTextAction("x"));

        // Unchanged rather than Rejected: typing where there is no property cell is the rule that already
        // existed for keywords and punctuation, and a placeholder is one more such cell.
        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Equal(version, rule.Model.Version);
    }
}
