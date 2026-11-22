using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>Extracting a node into a declared place, leaving a use of it behind.</summary>
/// <remarks>
/// The shape under test is the one a refactoring plan predicted and could not build out of a program: the node moves
/// and keeps its identity, and the use that takes its place has to point at exactly that node - which is why the three
/// mutations are one action. <see cref="ExtractLanguage"/> is the fixture, and <c>library</c> the destination.
/// </remarks>
public sealed class ExtractNodeActionTests
{
    [Fact]
    public void AStepLeavesItsChainIntoThePlaceTheActionNamesAndACallTakesItsSpot()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");
        var step = steps[0];

        using var session = ExtractLanguage.Open(container);

        var result = session.Apply(Extract(step.Id));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        // The definition: the same node, so every reference into its subtree and every caret address that names one
        // of its cells still lands.
        Assert.Same(step, Assert.Single(container.GetChildren("library")));

        var chain = container.GetChild("chain")!;
        var call = Assert.Single(chain.GetChildren("steps"));

        Assert.NotSame(step, call);
        Assert.Equal(ExtractLanguage.Call, call.Concept.Key);
        Assert.Equal(step.Id, call.GetReference("calls")!.Value.Target);

        // The use is named after the definition, because a node in a slot whose concept declares a name slot has to
        // have one and an extract cannot ask.
        Assert.Equal("build", call.Name);
    }

    [Fact]
    public void TheCallTakesTheIndexTheStepHeldAndTheSiblingsStayInOrder()
    {
        var (container, steps) = ExtractLanguage.WithSteps("one", "two");

        using var session = ExtractLanguage.Open(container);

        session.Apply(Extract(steps[0].Id));

        var remaining = container.GetChild("chain")!.GetChildren("steps");

        Assert.Equal(2, remaining.Count);
        Assert.Equal(ExtractLanguage.Call, remaining[0].Concept.Key);
        Assert.Same(steps[1], remaining[1]);
    }

    [Fact]
    public void AUseThatIsNotGivenTheNameKeepsTheOneItsConceptDeclares()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "library",
            ExtractLanguage.Call,
            "calls"));

        var call = Assert.Single(container.GetChild("chain")!.GetChildren("steps"));

        Assert.Equal("unnamed", call.Name);
    }

    [Fact]
    public void TheExtractedNodeGoesToTheEndOfTheDestinationSlot()
    {
        // A destination that reads as a list of declarations has one obvious place for what was just pulled out:
        // after what is already there.
        var (container, steps) = ExtractLanguage.WithSteps("one", "two");
        var already = container.Model.NewChild(container, "library", ExtractLanguage.Step);

        using var session = ExtractLanguage.Open(container);

        session.Apply(Extract(steps[0].Id));

        var library = container.GetChildren("library");

        Assert.Equal(2, library.Count);
        Assert.Same(already, library[0]);
        Assert.Same(steps[0], library[1]);
    }

    [Fact]
    public void TheCaretFollowsTheNodeIntoThePlaceItWasExtractedInto()
    {
        // The node kept its id through the move, so the address the caret names still exists - and it now stands in
        // the library, which is the answer the gesture asked for.
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == steps[0].Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));

        session.Apply(Extract(steps[0].Id));

        Assert.Equal(steps[0].Id, session.Selection.Caret.Address.Owner);
    }

    [Fact]
    public void UndoPutsEverythingBackInOneStep()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");
        var chain = container.GetChild("chain")!;

        using var session = ExtractLanguage.Open(container);

        session.Apply(Extract(steps[0].Id));
        session.History.Undo();

        Assert.Empty(container.GetChildren("library"));
        Assert.Same(steps[0], Assert.Single(chain.GetChildren("steps")));
        Assert.Same(chain, steps[0].Parent);
        Assert.Equal("steps", steps[0].ParentSlot);
    }

    [Fact]
    public void ANodeThatIsNotInsideAnythingCannotBeExtracted()
    {
        var step = ExtractLanguage.LoneStep();

        using var session = ExtractLanguage.Open(step);

        var result = session.Apply(Extract(step.Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("not inside anything", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeWithNoAncestorOfTheDeclaredConceptIsAReason()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        // Nothing is a call that holds a step, so the declaration is not about this node's enclosing tree.
        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Call,
            "calls",
            ExtractLanguage.Call,
            "calls"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("no 'Extract.Call' above this node", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ADestinationSlotTheConceptDoesNotDeclareIsAReason()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "nowhere",
            ExtractLanguage.Call,
            "calls"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("declares no child slot 'nowhere'", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeTheDestinationSlotCannotHoldIsAReason()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        // `chain` holds a run, and a step is not one.
        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "chain",
            ExtractLanguage.Call,
            "calls"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("cannot stand in slot 'chain'", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeAlreadyInTheDestinationIsAReason()
    {
        var (container, _) = ExtractLanguage.WithSteps("build");
        var extracted = container.Model.NewChild(container, "library", ExtractLanguage.Step);

        using var session = ExtractLanguage.Open(container);

        var result = session.Apply(Extract(extracted.Id));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("already stands in 'library'", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotThatHoldsOneChildCannotBeGivenASecond()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        container.Model.NewChild(container, "single", ExtractLanguage.Step);

        using var session = ExtractLanguage.Open(container);

        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "single",
            ExtractLanguage.Call,
            "calls"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("at most one child", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AUseConceptTheLanguageDoesNotHoldIsAReason()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "library",
            "Nope.Call",
            "calls"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("not part of the language", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AUseThatCannotStandWhereTheNodeStandsIsAReason()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        // A chain cannot take the place of a step, so the extract would have nowhere to leave its use.
        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "library",
            ExtractLanguage.Chain,
            "calls"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("cannot stand where", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AUseWithoutThatReferenceIsAReason()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "library",
            ExtractLanguage.Step,
            "nowhere"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("declares no reference named 'nowhere'", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ANamePropertyTheUseDoesNotDeclareIsAReason()
    {
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        var result = session.Apply(new ExtractNodeAction(
            steps[0].Id,
            ExtractLanguage.Container,
            "library",
            ExtractLanguage.Call,
            "calls",
            useName: "nowhere"));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Contains("declares no property named 'nowhere'", result.Reason, StringComparison.Ordinal);
    }

    /// <summary>Extracts a step into the container's library, as a call named after it.</summary>
    private static ExtractNodeAction Extract(NodeId step) =>
        new(step, ExtractLanguage.Container, "library", ExtractLanguage.Call, "calls", "name");
}
