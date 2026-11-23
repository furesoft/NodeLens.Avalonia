using Projectional.Core;
using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The name body vocabulary: what each part answers, and that a cycle answers nothing instead of crashing.</summary>
/// <remarks>
/// The parts are the declared form's whole vocabulary (docs/architecture/behaviours.md), so each one is proven here as
/// a part of a <see cref="NameBody"/> and once through a declaration - the seam a language actually uses.
/// </remarks>
public sealed class NameBodyTests
{
    private const string Node = "Nb.Node";
    private const string Leaf = "Nb.Leaf";
    private const string Link = "Nb.Link";
    private const string Loop = "Nb.Loop";

    [Fact]
    public void APropertyIsTheValueAndAnEmptySlotAnswersNothing()
    {
        var model = ModelWith(new NameProperty("amount"));
        var node = model.NewNode(Node);

        Assert.Equal(string.Empty, node.Name);

        node.Set("amount", "42");

        Assert.Equal("42", node.Name);
    }

    [Fact]
    public void AChildIsNamedAfterItsOwnConcept()
    {
        var model = ModelWith(new NameJoin(
            ".",
            new NameProperty("amount"),
            new NameChild("child")));

        var parent = model.NewNode(Node);
        parent.Set("amount", "left");
        model.NewChild(parent, "child", Leaf).Set("amount", "right");

        Assert.Equal("left.right", parent.Name);
    }

    [Fact]
    public void AReferenceIsNamedAfterItsTarget()
    {
        var model = ModelWith(new NameProperty("amount"));
        var target = model.NewNode(Node);

        target.Set("amount", "target");

        var link = model.NewNode(Link);

        link.SetReference("next", target);

        Assert.Equal("target", link.Name);
    }

    [Fact]
    public void AListOfChildrenIsNamedAfterAllOfThem()
    {
        // The shape one child cannot express: a route called after the places it stops at.
        var model = ModelWith(new NameChildren("children", " - "));
        var parent = model.NewNode(Node);

        model.NewChild(parent, "children", Leaf).Set("amount", "peak");
        model.NewChild(parent, "children", Leaf).Set("amount", "lake");

        Assert.Equal("peak - lake", parent.Name);
    }

    [Fact]
    public void AListLeavesOutTheChildrenThatAreNotNamedYet()
    {
        // The same omission the join makes, so a half-written list does not end in a separator.
        var model = ModelWith(new NameChildren("children", ", "));
        var parent = model.NewNode(Node);

        model.NewChild(parent, "children", Leaf).Set("amount", "peak");
        model.NewChild(parent, "children", Leaf);

        Assert.Equal("peak", parent.Name);
    }

    [Fact]
    public void AListReadsASlotThatHoldsOneChildJustAsWell()
    {
        // `children` on a slot that stores one child answers the names it holds, which is none or one.
        var model = ModelWith(new NameChildren("child", ", "));
        var parent = model.NewNode(Node);

        Assert.Equal(string.Empty, parent.Name);

        model.NewChild(parent, "child", Leaf).Set("amount", "only");

        Assert.Equal("only", parent.Name);
    }

    [Fact]
    public void AListNamingASlotTheConceptDoesNotDeclareIsALanguageDefect()
    {
        var model = ModelWith(new NameChildren("nothing", ", "));

        Assert.Throws<InvalidOperationException>(() => model.NewNode(Node).Name);
    }

    [Fact]
    public void AJoinLeavesOutTheEmptyAnswersAndTheirSeparator()    {
        // The reason the join omits rather than concatenates: a half-written node must not be called `left.`.
        var model = ModelWith(new NameJoin(
            ".",
            new NameProperty("amount"),
            new NameChild("child"),
            new NameProperty("kind")));

        var node = model.NewNode(Node);

        node.Set("amount", "left");
        node.Set("kind", "operation");

        Assert.Equal("left.operation", node.Name);
    }

    [Fact]
    public void AFallbackPicksTheFirstAnswerThatIsNotEmpty()
    {
        var model = ModelWith(new NameFallback(new NameProperty("amount"), new NameLiteral("anonymous")));
        var node = model.NewNode(Node);

        Assert.Equal("anonymous", node.Name);

        node.Set("amount", "named");

        Assert.Equal("named", node.Name);
    }

    [Fact]
    public void AChoicePicksByAPropertysValue()
    {
        var model = ModelWith(new NameChoice(
            "kind",
            "operation",
            new NameJoin(".", new NameProperty("amount"), new NameLiteral("call")),
            new NameProperty("amount")));

        var node = model.NewNode(Node);

        node.Set("amount", "value");
        Assert.Equal("value", node.Name);

        node.Set("kind", "operation");
        Assert.Equal("value.call", node.Name);
    }

    [Fact]
    public void ACycleAnswersNothingRatherThanOverflowingTheStack()
    {
        // A name that reads its own reference target can come back to itself; the depth budget is what makes that a
        // short answer instead of a StackOverflowException nobody can be told about.
        var model = ModelWith(new NameProperty("amount"));
        var loop = model.NewNode(Loop);

        loop.SetReference("next", loop);

        Assert.Equal(string.Empty, loop.Name);
    }

    [Fact]
    public void TheDeclarationTakesTheFunctionAndNodeNameAnswersIt()
    {
        var model = ModelWith(new NameLiteral("computed"));

        Assert.Equal("computed", model.NewNode(Node).Name);
    }

    [Fact]
    public void ASlotTheConceptDoesNotDeclareIsALanguageDefect()
    {
        // A half-written document is total, but a name body naming a slot its concept does not have is a defect, and
        // saying so beats answering a silently short name. The declared form refuses it at load instead.
        var model = ModelWith(new NameProperty("nothing"));

        Assert.Throws<InvalidOperationException>(() => model.NewNode(Node).Name);
    }

    private static Model ModelWith(NameBody body) =>
        new Project(ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                Node,
                [
                    new PropertySlot("amount", PropertyType.String),
                    new PropertySlot("kind", PropertyType.String),
                    new ChildSlot("child", Leaf, Cardinality.One),
                    new ChildSlot("children", Leaf, Cardinality.Many),
                    new ReferenceSlot("next", Node, allowOwnSubtree: true),
                ],
                nameOf: body.ToFunction()),

            // A child whose name is its own property, so a name built out of children is a name built out of their
            // answers rather than out of the parent's body asked again.
            new ConceptDeclaration(
                Leaf,
                [new PropertySlot("amount", PropertyType.String)],
                nameOf: new NameProperty("amount").ToFunction()),

            new ConceptDeclaration(
                Link,
                [new ReferenceSlot("next", Node, allowOwnSubtree: true)],
                nameOf: new NameReference("next").ToFunction()),

            new ConceptDeclaration(
                Loop,
                [new ReferenceSlot("next", Loop, allowOwnSubtree: true)],
                nameOf: new NameReference("next").ToFunction()),
        ])).NewModel("names");
}
