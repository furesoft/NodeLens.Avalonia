using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Sorting one list slot by name: the rule, and what it refuses.</summary>
/// <remarks>
/// The rule itself is pinned in <see cref="ChildOrderTests"/>; here it is the action that matters - the order really
/// changes, the caret follows the node it was in rather than staying at an index, one press undoes it, and a list
/// that is already in order is not an edit at all.
/// </remarks>
public sealed class SortChildrenActionTests
{
    [Fact]
    public void TheListIsPutInNameOrder()
    {
        var (ruleset, rules) = Ruleset("gamma", "alpha", "beta");

        using var session = Session(ruleset);

        Assert.Equal(EditOutcome.Applied, session.Apply(new SortChildrenAction(ruleset.Id, RulesLanguage.RulesSlot)).Outcome);

        Assert.Equal(["alpha", "beta", "gamma"], Names(ruleset));

        // The same nodes, in another order - nothing was recreated.
        Assert.Equal(
            [rules[1].Id, rules[2].Id, rules[0].Id],
            [.. ruleset.GetChildren(RulesLanguage.RulesSlot).Select(rule => rule.Id)]);
    }

    [Fact]
    public void TheCaretFollowsTheNodeItWasIn()
    {
        var (ruleset, rules) = Ruleset("gamma", "alpha", "beta");

        using var session = Session(ruleset);
        Caret(session, rules[0]);

        session.Apply(new SortChildrenAction(ruleset.Id, RulesLanguage.RulesSlot));

        // "gamma" was first and is last now; the caret went with it instead of staying at index 0, which would have
        // left the user standing in "alpha".
        Assert.Equal(2, ruleset.GetChildren(RulesLanguage.RulesSlot)[2].IndexInParent);
        Assert.Equal(rules[0].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void ASortIsOneUndoStep()
    {
        var (ruleset, _) = Ruleset("gamma", "alpha");

        using var session = Session(ruleset);

        session.Apply(new SortChildrenAction(ruleset.Id, RulesLanguage.RulesSlot));

        Assert.True(session.Undo());
        Assert.Equal(["gamma", "alpha"], Names(ruleset));
    }

    [Fact]
    public void AListThatIsAlreadyInOrderIsNotAnEdit()
    {
        var (ruleset, _) = Ruleset("alpha", "beta");

        using var session = Session(ruleset);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(new SortChildrenAction(ruleset.Id, RulesLanguage.RulesSlot)).Outcome);
        Assert.False(session.Undo());
    }

    [Fact]
    public void ASlotThatHoldsOneChildIsRejected()
    {
        var model = RulesLanguage.NewProject().NewModel("single");
        var rule = model.NewNode(RulesConceptKeys.Rule);
        model.AddRoot(rule);
        var condition = model.NewChild(rule, "condition", ExpressionsConceptKeys.Name);

        using var session = new EditorSession(
            rule,
            RulesLanguage.CreateNotations(RulesLanguage.Concepts));

        var result = session.Apply(new SortChildrenAction(rule.Id, "condition"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Equal(condition.Id, rule.GetChild("condition")!.Id);
    }

    [Fact]
    public void ASlotThatDoesNotExistIsRejected()
    {
        var (ruleset, _) = Ruleset("alpha", "beta");

        using var session = Session(ruleset);

        var result = session.Apply(new SortChildrenAction(ruleset.Id, "nope"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void ANodeThatIsGoneIsRejected()
    {
        var (ruleset, _) = Ruleset("alpha", "beta");

        using var session = Session(ruleset);

        var result = session.Apply(new SortChildrenAction(NodeId.New(), RulesLanguage.RulesSlot));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    /// <summary>A ruleset whose rules carry <paramref name="names"/>, with the rules in that order.</summary>
    private static (Node Ruleset, IReadOnlyList<Node> Rules) Ruleset(params string[] names)
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        model.AddRoot(ruleset);

        List<Node> rules = [];

        foreach (var name in names)
        {
            var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
            rule.Set("name", name);
            rules.Add(rule);
        }

        return (ruleset, rules);
    }

    private static EditorSession Session(Node document) =>
        new(document, RulesLanguage.CreateNotations(RulesLanguage.Concepts));

    /// <summary>Puts the caret on the first cell <paramref name="node"/> owns.</summary>
    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    private static IReadOnlyList<string?> Names(Node ruleset) =>
        [.. ruleset.GetChildren(RulesLanguage.RulesSlot).Select(rule => rule.GetOrNull("name"))];
}
