using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A "while this slot is filled" cell draws its cells only while a child slot of the node holds a child, and
/// otherwise draws nothing at all - inline inside a line, or as a block that leaves no row behind.
/// </summary>
/// <remarks>
/// Same negative half as the property conditional, for a different reason: a slot that is empty is usually a slot
/// with a placeholder of its own, and the point of this cell is that there is no placeholder - the language hides
/// the whole statement. What follows from that is the interesting part: the empty slot has no cell, so a language
/// that uses this cell owes a second way in, and the structure language pays with two intentions.
/// </remarks>
public sealed class WhenFilledNotationTests
{
    private const string Rule = "C.Rule";

    [Fact]
    public void TheCellsAreDrawnWhileTheSlotHoldsAChild()
    {
        var drawn = Render(
            ed => ed.Line(ed.WhenFilled("body", ed.Keyword("has"), ed.Space), ed.Keyword("rule")),
            filled: true);

        Assert.Equal("has rule", drawn.Trim());
    }

    [Fact]
    public void NothingIsDrawnWhileItIsEmpty()
    {
        var drawn = Render(
            ed => ed.Line(ed.WhenFilled("body", ed.Keyword("has"), ed.Space), ed.Keyword("rule")),
            filled: false);

        Assert.Equal("rule", drawn.Trim());
    }

    [Fact]
    public void AnEmptySlotLeavesNoCaretStopBehind()
    {
        // The cell draws nothing, so nothing addresses the slot: not an empty cell, not a space. That is what makes
        // the language owe an intention - there is no place a caret could stand to create the first child.
        var concepts = Concepts();
        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, ed => ed.Line(ed.WhenFilled("body", ed.Keyword("has"), ed.Space), ed.Keyword("rule")));

        var model = new Project(concepts).NewModel();
        var rule = model.NewNode(Rule);
        var tree = new EditorTreeBuilder(notations).Build(rule);

        Assert.DoesNotContain(
            CaretPlaces.Of(tree),
            place => place.Address.Equals(CellAddress.OfSlot(rule.Id, "body")));
    }

    [Fact]
    public void AWhenFilledOnASlotTheConceptDoesNotDeclareIsRefused()
    {
        // A slot nobody declared can never be filled, so the cells could never be drawn - a mistake that is
        // invisible at run time and one word at registration.
        var error = Refuses(ed => ed.Line(ed.WhenFilled("missing", ed.Keyword("has"))));

        Assert.Contains("'missing'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declares no such child", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWhenFilledOnAPropertyIsRefused()
    {
        // A property is not a slot with children, and accepting the spelling would make the cell silently never
        // draw - which reads exactly like a notation that forgot to say anything.
        var error = Refuses(ed => ed.Line(ed.WhenFilled("name", ed.Keyword("has"))));

        Assert.Contains("'name'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declares no such child", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWhenFilledInASeparatorIsRefusedWhenItDrawsNoCell()
    {
        // The same rule the conditional follows: a separator has to be exactly one cell, so a cell that can draw
         // nothing there is a notation that draws a different gap than the one that was written. The condition reads
        // a slot that is empty while the collection it separates is not, which is the shape that draws no cell.
        var concepts = Concepts();
        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, ed => ed.Collection(
            "body",
            ed.WhenFilled("head", ed.Punct(","))));

        var model = new Project(concepts).NewModel();
        var rule = model.NewNode(Rule);

        model.NewChild(rule, "body", Rule);
        model.NewChild(rule, "body", Rule);

        Assert.Throws<ProjectionException>(() => LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ABlockIsDrawnWhileTheSlotHoldsAChild()
    {
        var drawn = Render(
            ed => ed.Sequence(
                ed.Line(ed.Keyword("rule")),
                ed.WhenFilledBlock("body", ed.Line(ed.Keyword("has")))),
            filled: true);

        Assert.Equal("rule\nhas", drawn);
    }

    [Fact]
    public void AnEmptyBlockLeavesNoRowAtAll()
    {
        // The difference to the inline cell, and the whole reason the block form exists: an empty line would still
        // take the row layout reserves for it, so an emptied block has to leave the tree instead.
        var drawn = Render(
            ed => ed.Sequence(
                ed.Line(ed.Keyword("rule")),
                ed.WhenFilledBlock("body", ed.Line(ed.Keyword("has")))),
            filled: false);

        Assert.Equal("rule", drawn);
    }

    [Fact]
    public void AWhenFilledBlockOnASlotTheConceptDoesNotDeclareIsRefused()
    {
        var error = Refuses(ed => ed.Sequence(ed.WhenFilledBlock("missing", ed.Line(ed.Keyword("has")))));

        Assert.Contains("'missing'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declares no such child", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWhenFilledBlockOnAPropertyIsRefused()
    {
        var error = Refuses(ed => ed.Sequence(ed.WhenFilledBlock("name", ed.Line(ed.Keyword("has")))));

        Assert.Contains("'name'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declares no such child", error.Message, StringComparison.Ordinal);
    }

    private static string Render(Func<NotationEditor, BlockNotationCell> build, bool filled)
    {
        var concepts = Concepts();
        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, build);

        var model = new Project(concepts).NewModel();
        var rule = model.NewNode(Rule);

        if (filled)
        {
            model.NewChild(rule, "body", Rule);
        }

        return LayoutSnapshot.Render(rule, notations, width: 40);
    }

    private static NotationException Refuses(Func<NotationEditor, BlockNotationCell> build)
    {
        var notations = new NotationRegistry(Concepts());

        return Assert.Throws<NotationException>(() => notations.Add(Rule, build));
    }

    private static ConceptRegistry Concepts() => ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Rule,
            [
                new PropertySlot("name", PropertyType.String),
                new ChildSlot("body", Rule, Cardinality.Many),
                new ChildSlot("head", Rule, Cardinality.Many),
            ]),
    ]);
}
