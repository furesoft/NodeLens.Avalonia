using Projectional.Expressions;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What changed between two models, matched by node id - the answer a preview's ghost is drawn from.
/// </summary>
/// <remarks>
/// Every test diffs a model against a copy of itself, which is exactly how a preview uses this: the copy is
/// the sandbox, one edit is applied to it, and the difference is what has to be marked. Starting from an
/// identical copy is also the only honest way to prove "nothing changed" means nothing.
/// </remarks>
public sealed class ModelDiffTests
{
    [Fact]
    public void ACopyDiffersInNothing()
    {
        var original = PayrollSample.NewSampleModel();

        Assert.Empty(ModelDiff.Of(original, original.CopyTo(new Project(RulesLanguage.Concepts))));
    }

    [Fact]
    public void AChangedPropertyIsReportedWithBothValues()
    {
        var (original, sandbox) = Sandbox();
        var rule = sandbox.Project.FindNode(Rule(original).Id)!;

        rule.Set("name", "Renamed");

        var difference = Assert.Single(ModelDiff.Of(original, sandbox));

        Assert.Equal(NodeDifferenceKind.PropertyChanged, difference.Kind);
        Assert.Equal(rule.Id, difference.Node);
        Assert.Equal("name", difference.Slot);
        Assert.Equal("Approved", difference.OldValue);
        Assert.Equal("Renamed", difference.NewValue);
    }

    [Fact]
    public void WritingTheSameValueAgainIsNotADifference()
    {
        var (original, sandbox) = Sandbox();

        sandbox.Project.FindNode(Rule(original).Id)!.Set("name", "Approved");

        Assert.Empty(ModelDiff.Of(original, sandbox));
    }

    [Fact]
    public void AnAddedNodeIsReportedAsAdded()
    {
        var (original, sandbox) = Sandbox();
        var added = sandbox.NewChild(sandbox.Roots[0], RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        added.Set("name", "Second");

        var difference = Assert.Single(ModelDiff.Of(original, sandbox));

        Assert.Equal(NodeDifferenceKind.Added, difference.Kind);
        Assert.Equal(added.Id, difference.Node);
        Assert.Equal(RulesLanguage.RulesSlot, difference.Slot);
        Assert.Equal(NodePlace.Of(added), difference.After);
    }

    [Fact]
    public void AnAddedSubtreeIsReportedNodeByNode()
    {
        var (original, sandbox) = Sandbox();
        var ruleset = sandbox.Roots[0];
        var added = sandbox.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        var condition = sandbox.NewChild(added, "condition", ExpressionsConceptKeys.IntLiteral);

        condition.Set("value", 7);

        var differences = ModelDiff.Of(original, sandbox);

        Assert.Equal([added.Id, condition.Id], differences.Select(each => each.Node));
        Assert.All(differences, each => Assert.Equal(NodeDifferenceKind.Added, each.Kind));
    }

    [Fact]
    public void ARemovedSubtreeIsReportedNodeByNodeAndNamesTheRoot()
    {
        var (original, sandbox) = Sandbox();
        var rule = Rule(original);
        var condition = rule.GetChild("condition")!;

        sandbox.Roots[0].RemoveChild(RulesLanguage.RulesSlot, sandbox.Project.FindNode(rule.Id)!);

        var differences = ModelDiff.Of(original, sandbox);

        // The rule, its condition and the two operands below it: everything that left the document.
        Assert.Equal(4, differences.Count);
        Assert.All(differences, each => Assert.Equal(NodeDifferenceKind.Removed, each.Kind));
        Assert.Contains(condition.Id, differences.Select(each => each.Node));

        // The root of the removal keeps its name, so a badge can say what went without the old model.
        Assert.Equal("Approved", Assert.Single(differences, each => each.Node == rule.Id).OldValue);
        Assert.Equal(NodePlace.Of(rule), Assert.Single(differences, each => each.Node == rule.Id).Before);
    }

    [Fact]
    public void AMoveIsAMoveAndNotADeletePlusAnInsert()
    {
        // Both rules have to exist in both models for a move to be observable, so the second one is added
        // before the copy is taken.
        var original = PayrollSample.NewSampleModel();
        var ruleset = original.Roots[0];
        var second = original.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        second.Set("name", "Second");

        var sandbox = original.CopyTo(new Project(RulesLanguage.Concepts));
        var copiedRuleset = sandbox.Roots[0];
        var copiedSecond = sandbox.Project.FindNode(second.Id)!;

        copiedRuleset.RemoveChild(RulesLanguage.RulesSlot, copiedSecond);
        copiedRuleset.InsertChild(RulesLanguage.RulesSlot, 0, copiedSecond);

        var differences = ModelDiff.Of(original, sandbox);

        // Swapping two rules moves both of them, and neither is a new node or a deleted one.
        Assert.DoesNotContain(differences, each => each.Kind is NodeDifferenceKind.Added or NodeDifferenceKind.Removed);
        Assert.Equal(2, differences.Count(each => each.Kind == NodeDifferenceKind.Moved));

        var moved = Assert.Single(differences, each => each.Node == second.Id);

        Assert.Equal(1, moved.Before!.Value.Index);
        Assert.Equal(0, moved.After!.Value.Index);
    }

    [Fact]
    public void ARemovalComesAfterTheChangesSoTheGhostCanDrawFirst()
    {
        var (original, sandbox) = Sandbox();
        var ruleset = sandbox.Roots[0];

        sandbox.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule).Set("name", "Second");
        ruleset.RemoveChild(RulesLanguage.RulesSlot, sandbox.Project.FindNode(Rule(original).Id)!);

        var differences = ModelDiff.Of(original, sandbox);

        // What exists is reported first, what is gone last: a renderer can walk the list once, marking the
        // document it is drawing and collecting the removals for the badge on the way.
        Assert.Equal(NodeDifferenceKind.Added, differences[0].Kind);
        Assert.All(
            differences.Where(each => each.Kind != NodeDifferenceKind.Added),
            each => Assert.Equal(NodeDifferenceKind.Removed, each.Kind));
    }

    [Fact]
    public void AChangedReferenceIsReportedWithBothTargets()
    {
        var source = ReferenceModel();
        var sandbox = source.CopyTo(new Project(TestLanguage.Registry));
        var rule = sandbox.Project.FindNode(ReferencingRule(source).Id)!;
        var later = sandbox.Roots[1];

        rule.SetReference("tag", later);

        var difference = Assert.Single(ModelDiff.Of(source, sandbox));

        Assert.Equal(NodeDifferenceKind.ReferenceChanged, difference.Kind);
        Assert.Equal("tag", difference.Slot);
        Assert.Contains("urgent", difference.OldValue!, StringComparison.Ordinal);
        Assert.Contains(later.Id.ToString(), difference.NewValue!, StringComparison.Ordinal);
    }

    [Fact]
    public void RespellingAReferenceWithoutMovingItIsStillAChange()
    {
        // The fallback text is what an unresolved reference degrades into, so it is part of what a reference
        // says - a preview that ignored it would show a reference that reads differently as unchanged.
        var source = ReferenceModel();
        var sandbox = source.CopyTo(new Project(TestLanguage.Registry));
        var rule = sandbox.Project.FindNode(ReferencingRule(source).Id)!;
        var urgent = sandbox.Roots[0];

        rule.SetReference("tag", urgent.Id, "spelled-differently");

        var difference = Assert.Single(ModelDiff.Of(source, sandbox));

        Assert.Equal(NodeDifferenceKind.ReferenceChanged, difference.Kind);
        Assert.Contains("spelled-differently", difference.NewValue!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameIdOnAnotherConceptIsNotTheSameNode()
    {
        // Ids are unique per project, not per universe: two unrelated models can carry one id for different
        // concepts. Reporting that as a property change would describe an edit nobody made.
        var original = PayrollSample.NewSampleModel();
        var rule = Rule(original);

        var other = new Project(TestLanguage.Registry).NewModel("other");
        other.AddRoot(other.CreateDetached(rule.Id, TestLanguage.Registry.Get(TestLanguage.Tag)));

        var differences = ModelDiff.Of(original, other);

        Assert.Contains(differences, each => each.Node == rule.Id && each.Kind == NodeDifferenceKind.Added);
        Assert.Contains(differences, each => each.Node == rule.Id && each.Kind == NodeDifferenceKind.Removed);
    }

    [Fact]
    public void DiffingNothingIsRefused()
    {
        var model = PayrollSample.NewSampleModel();

        Assert.Throws<ArgumentNullException>(() => ModelDiff.Of(null!, model));
        Assert.Throws<ArgumentNullException>(() => ModelDiff.Of(model, null!));
    }

    /// <summary>The sample model and an untouched copy of it in a project of its own.</summary>
    private static (Model Original, Model Sandbox) Sandbox()
    {
        var original = PayrollSample.NewSampleModel();

        return (original, original.CopyTo(new Project(RulesLanguage.Concepts)));
    }

    /// <summary>Two tags and a rule pointing at the first, which is what a reference difference needs.</summary>
    private static Model ReferenceModel()
    {
        var model = new Project(TestLanguage.Registry).NewModel("references");
        var urgent = model.NewNode(TestLanguage.Tag);

        urgent.Set("label", "urgent");
        model.AddRoot(urgent);

        var later = model.NewNode(TestLanguage.Tag);

        later.Set("label", "later");
        model.AddRoot(later);

        var rule = model.NewNode(TestLanguage.Rule);

        rule.Set("name", "Only");
        rule.SetReference("tag", urgent);
        model.AddRoot(rule);

        return model;
    }

    private static Node Rule(Model model) =>
        model.Roots[0].GetChildren(RulesLanguage.RulesSlot)[0];

    /// <summary>The rule of <see cref="ReferenceModel"/>, which sits behind its two tags.</summary>
    private static Node ReferencingRule(Model model) => model.Roots[2];
}
