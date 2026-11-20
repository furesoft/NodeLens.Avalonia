using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A concept may compute what its nodes are called, and every reader gets that one answer.</summary>
/// <remarks>
/// The seam is <see cref="Concept.NameOf"/>, which <see cref="Node.Name"/> asks, so the nine consumers - a reference
/// cell, sort by name, the outline, the problems panel, the scope, ambiguity, the stored fallback text, rename and the
/// naming checks - read a computed name without knowing that one exists (docs/architecture/behaviours.md). The three
/// shapes here are the ones a name slot cannot express: a name that is a value, a name spread over a child, and a
/// name inherited from a super concept.
/// </remarks>
public sealed class ConceptNameTests
{
    private const string Item = "Name.Item";
    private const string Pair = "Name.Pair";
    private const string Base = "Name.Base";
    private const string Derived = "Name.Derived";
    private const string Both = "Name.Both";
    private const string Plain = "Name.Plain";
    private const string List = "Name.List";

    [Fact]
    public void AConceptThatComputesItsNameAnswersItThroughNodeName()
    {
        var node = Model().NewNode(Item);

        node.Set("amount", "3");

        Assert.Equal("item 3", node.Name);
    }

    [Fact]
    public void AConceptWithNeitherAFunctionNorASlotAnswersNoName()
    {
        Assert.Null(Model().NewNode(Plain).Name);
    }

    [Fact]
    public void TheFunctionWinsAndTheSlotStaysReadable()
    {
        // A concept that declares both keeps its slot - that is what a slot declared over `Framework.Named` reads -
        // while the drawing and the completion show the computed word.
        var node = Model().NewNode(Both);

        node.Set("name", "stored");

        Assert.Equal("computed", node.Name);
        Assert.Equal("stored", node.GetOrNull("name"));
    }

    [Fact]
    public void ASubconceptInheritsTheFunction()
    {
        var node = Model().NewNode(Derived);

        node.Set("amount", "7");

        Assert.Equal("item 7", node.Name);
        Assert.NotNull(node.Concept.NameFunction);
    }

    [Fact]
    public void ACompositeNameReadsAChildsName()
    {
        // The shape a name slot cannot express: the interesting words are in two places, one of them a child.
        var model = Model();
        var pair = model.NewNode(Pair);

        pair.Set("first", "left");
        model.NewChild(pair, "second", Item).Set("amount", "2");

        Assert.Equal("left.item 2", pair.Name);
    }

    [Fact]
    public void SortByComputedNameOrdersTheList()
    {
        var model = Model();
        var list = model.NewNode(List);

        foreach (var amount in (string[])["3", "1"])
        {
            model.NewChild(list, "items", Item).Set("amount", amount);
        }

        var order = ChildOrder.ByName(list, "items");

        Assert.NotNull(order);
        Assert.Equal(["item 1", "item 3"], order.Select(child => child.Name));
    }

    [Fact]
    public void SortRefusesAListWhoseChildrenAnswerNoName()
    {
        var model = Model();
        var list = model.NewNode(List);

        model.NewChild(list, "plain", Plain);
        model.NewChild(list, "plain", Plain);

        Assert.Null(ChildOrder.ByName(list, "plain"));
    }

    private static Model Model()
    {
        var project = new Project(Registry());

        return project.NewModel("name");
    }

    private static ConceptRegistry Registry() => ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Item,
            [new PropertySlot("amount", PropertyType.String)],
            nameOf: node => $"item {node.GetOrNull("amount")}"),

        new ConceptDeclaration(
            Pair,
            [new PropertySlot("first", PropertyType.String), new ChildSlot("second", Item, Cardinality.One)],
            nameOf: node => $"{node.GetOrNull("first")}.{node.GetChild("second")?.Name}"),

        new ConceptDeclaration(
            Base,
            [new PropertySlot("amount", PropertyType.String)],
            isAbstract: true,
            nameOf: node => $"item {node.GetOrNull("amount")}"),

        new ConceptDeclaration(Derived, superConcepts: [Base]),

        new ConceptDeclaration(
            Both,
            [new PropertySlot("name", PropertyType.String)],
            nameSlot: "name",
            nameOf: _ => "computed"),

        new ConceptDeclaration(Plain, [new PropertySlot("label", PropertyType.String)]),

        new ConceptDeclaration(
            List,
            [
                new ChildSlot("items", Item, Cardinality.Many),
                new ChildSlot("plain", Plain, Cardinality.Many),
            ]),
    ]);
}
