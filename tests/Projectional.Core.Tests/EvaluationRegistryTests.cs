using Projectional.Language;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// How the evaluation registry answers "which rule applies": its own, the nearest super concept's, or nothing.
/// </summary>
/// <remarks>
/// The lookup is the same one notation, triggers, scope providers, type rules and constraints use, so a
/// language has one answer to "which declaration applies". The one case that differs from a type is the last:
/// a concept with no rule has no value at all, because there is no "unknown" to hand back, so the run fails
/// instead of inventing one.
/// </remarks>
public sealed class EvaluationRegistryTests
{
    private static readonly Value Marker = new StringValue("marker");

    [Fact]
    public void AConceptAnswersWithItsOwnRule()
    {
        var item = GroupEditor.GroupWithItems("first").Items[0];

        var result = Run(Marker, GroupLanguage.Item, item);

        Assert.Equal(Marker, result.Value);
        Assert.Equal(1, result.Steps);
    }

    [Fact]
    public void AConceptInheritsTheRuleOfItsSuper()
    {
        var group = GroupEditor.GroupWithItems("first").Group;

        var result = Run(Marker, GroupLanguage.Item, group);

        Assert.Equal(Marker, result.Value);
    }

    [Fact]
    public void AConceptThatDeclaresNothingHasNoValue()
    {
        var item = GroupEditor.GroupWithItems("first").Items[0];

        var result = Run(Marker, GroupLanguage.Group, item);

        Assert.False(result.Succeeded);
        Assert.Equal(InterpretationFailureKind.Evaluation, result.Failure!.Kind);
        Assert.Contains("no evaluation rule", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyRegistryEvaluatesNothing()
    {
        var item = GroupEditor.GroupWithItems("first").Items[0];

        var result = new Interpreter(EvaluationRegistry.Empty).Run(item, cancellationToken: Token);

        Assert.False(result.Succeeded);
        Assert.Equal(0, EvaluationRegistry.Empty.Count);
    }

    [Fact]
    public void AddingTheSameConceptAgainReplacesTheRule()
    {
        var replaced = new StringValue("replaced");
        var registry = EvaluationRegistry.Empty
            .Add(GroupLanguage.Item, new DelegateEvaluationRule(_ => Marker))
            .Add(GroupLanguage.Item, new DelegateEvaluationRule(_ => replaced));

        var item = GroupEditor.GroupWithItems("first").Items[0];

        Assert.Equal(1, registry.Count);
        Assert.Equal(replaced, new Interpreter(registry).Run(item, cancellationToken: Token).Value);
    }

    [Fact]
    public void AnEmptyConceptKeyIsRefused()
    {
        Assert.Throws<ArgumentException>(
            () => EvaluationRegistry.Empty.Add(string.Empty, new DelegateEvaluationRule(_ => Marker)));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static InterpretationResult Run(Value value, string conceptKey, Core.Node node) =>
        new Interpreter(EvaluationRegistry.Empty.Add(conceptKey, new DelegateEvaluationRule(_ => value)))
            .Run(node, cancellationToken: Token);
}
