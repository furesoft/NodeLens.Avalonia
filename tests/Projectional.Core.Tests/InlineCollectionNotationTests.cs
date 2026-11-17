using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The inline collection cell: a separated list that stays inside the line it is drawn on, which is what
/// lets a notation write <c>[a, b, c]</c> or a record's fields without pushing every item onto its own line.
/// </summary>
public sealed class InlineCollectionNotationTests
{
    private const string List = "Inline.List";
    private const string Item = "Inline.Item";
    private const string Wrapper = "Inline.Wrapper";

    private static readonly ConceptRegistry Concepts = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            List,
            [new ChildSlot("items", Item, Cardinality.Many)],
            description: "A bracket list."),

        new ConceptDeclaration(
            Item,
            [new PropertySlot("name", PropertyType.String)],
            nameSlot: "name",
            description: "One item of a list."),

        new ConceptDeclaration(
            Wrapper,
            [new ChildSlot("inner", List, Cardinality.One)],
            description: "Something that wraps a list, to prove the list did not start a line of its own."),
    ]);

    private static NotationRegistry Notations => Create();

    [Fact]
    public void AListStaysOnTheLineItWasDrawnIn()
    {
        var model = new Project(Concepts).NewModel("inline");
        var list = model.NewNode(List);

        AddItem(model, list, "a");
        AddItem(model, list, "b");
        AddItem(model, list, "c");

        Assert.Equal("[a, b, c]", LayoutSnapshot.Render(model, Notations, width: 40));
    }

    [Fact]
    public void AWrappedListDoesNotStartALineOfItsOwn()
    {
        var model = new Project(Concepts).NewModel("inline");
        var wrapper = model.NewNode(Wrapper);
        var list = model.NewChild(wrapper, "inner", List);

        AddItem(model, list, "a");
        AddItem(model, list, "b");

        // The whole point: a block collection here would have broken the line at the opening angle.
        Assert.Equal("<[a, b]>", LayoutSnapshot.Render(model, Notations, width: 40));
    }

    [Fact]
    public void OneItemHasNoSeparator()
    {
        var model = new Project(Concepts).NewModel("inline");
        var list = model.NewNode(List);

        AddItem(model, list, "only");

        Assert.Equal("[only]", LayoutSnapshot.Render(model, Notations, width: 40));
    }

    [Fact]
    public void AnEmptyListShowsItsPlaceholder()
    {
        var model = new Project(Concepts).NewModel("inline");
        model.NewNode(List);

        // An empty slot stays visible and fillable - the rule every slot-bearing cell follows.
        Assert.Equal("[<< ... >>]", LayoutSnapshot.Render(model, Notations, width: 40));
    }

    [Fact]
    public void AnInlineCollectionOverASlotTheConceptDoesNotDeclareIsRefusedAtRegistration()
    {
        var notations = new NotationRegistry(Concepts);

        var exception = Assert.Throws<NotationException>(() => notations.Add(
            List,
            ed => ed.Line(ed.InlineCollection("missing", ed.Punct(",")))));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativeSpacingIsRefusedWhenTheCellIsBuilt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new InlineCollectionNotationCell("items") { Spacing = -1 });
    }

    private static void AddItem(Model model, Node list, string name) =>
        model.NewChild(list, "items", Item).Set("name", name);

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(Concepts);

        notations.Add(List, ed => ed.Line(
            ed.Keyword("["),
            ed.InlineCollection("items", ed.Punct(","), "<< ... >>"),
            ed.Keyword("]")));

        notations.Add(Item, ed => ed.Line(ed.Property("name")));

        notations.Add(Wrapper, ed => ed.Line(
            ed.Keyword("<"),
            ed.ChildInline("inner"),
            ed.Keyword(">")));

        return notations;
    }
}
