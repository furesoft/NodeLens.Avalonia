using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A conditional cell draws its cells only while a property of the node holds a value, and otherwise draws
/// nothing at all.
/// </summary>
/// <remarks>
/// The shape under test is MPS': <c>abstract concept Rule</c> and <c>concept Rule</c> are the same concept and the
/// same notation, and the word <c>abstract</c> exists only while the flag is true. The interesting half is
/// therefore the negative one - a conditional that does not hold must leave no cell behind, because a cell with no
/// text is a caret stop pointing at nothing.
/// </remarks>
public sealed class ConditionalNotationTests
{
    private const string Rule = "C.Rule";

    [Fact]
    public void TheCellsAreDrawnWhileThePropertyHolds()
    {
        var drawn = Render(
            ed => ed.Line(ed.Conditional("enabled", "true", ed.Keyword("on"), ed.Space), ed.Keyword("rule")),
            enabled: true);

        Assert.Equal("on rule", drawn.Trim());
    }

    [Fact]
    public void NothingIsDrawnWhileItDoesNot()
    {
        var drawn = Render(
            ed => ed.Line(ed.Conditional("enabled", "true", ed.Keyword("on"), ed.Space), ed.Keyword("rule")),
            enabled: false);

        Assert.Equal("rule", drawn.Trim());
    }

    [Fact]
    public void AConditionalReadsTheDefaultOfAPropertyNobodySet()
    {
        // The value is not stored, only defaulted, and a language that writes a default means it: `enabled`
        // starts out true, so the word is there before anybody touched anything.
        var concepts = Concepts();
        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, ed => ed.Line(ed.Conditional("enabled", "true", ed.Keyword("on"), ed.Space), ed.Keyword("rule")));

        var model = new Project(concepts).NewModel();
        var rule = model.NewNode(Rule);

        Assert.Equal("on rule", LayoutSnapshot.Render(rule, notations, width: 40).Trim());
    }

    [Fact]
    public void TheValueOfAConditionIsNotACellOfItsOwn()
    {
        // The flag moved into the header, so it has no cell and no caret stop: the notation draws the word or
        // draws nothing, and either way there is nowhere to type a truth value.
        var concepts = Concepts();
        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, ed => ed.Line(ed.Conditional("enabled", "true", ed.Keyword("on"), ed.Space), ed.Keyword("rule")));

        var model = new Project(concepts).NewModel();
        var rule = model.NewNode(Rule);
        var tree = new EditorTreeBuilder(notations).Build(rule);

        Assert.DoesNotContain(CaretPlaces.Of(tree), place => place.Cell is PropertyCell);
    }

    [Fact]
    public void AConditionOnAPropertyTheConceptDoesNotDeclareIsRefused()
    {
        var error = Refuses(ed => ed.Line(ed.Conditional("missing", "true", ed.Keyword("on"))));

        Assert.Contains("'missing'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declares no such property", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConditionOnAChildSlotIsRefused()
    {
        // A child has no value to compare, and accepting the spelling would make the cell silently never draw.
        var error = Refuses(ed => ed.Line(ed.Conditional("body", "true", ed.Keyword("on"))));

        Assert.Contains("declares no such property", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConditionOnAValueTheTypeWouldRefuseIsRefused()
    {
        // "yes" can never be what a truth value holds, so the cells could never be drawn - a mistake that is
        // invisible at run time and one word at registration.
        var error = Refuses(ed => ed.Line(ed.Conditional("enabled", "yes", ed.Keyword("on"))));

        Assert.Contains("\"yes\"", error.Message, StringComparison.Ordinal);
        Assert.Contains("'bool'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AConditionalInASeparatorIsRefusedWhenItDrawsNoCell()
    {
        // A collection's separator has to be exactly one cell, so a conditional there is not a notation the
        // renderer may quietly reshape: it is a notation that draws a different gap than the one that was written.
        var concepts = Concepts();
        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, ed => ed.Collection(
            "body",
            ed.Conditional("enabled", "false", ed.Punct(","))));

        var model = new Project(concepts).NewModel();
        var rule = model.NewNode(Rule);

        model.NewChild(rule, "body", Rule);
        model.NewChild(rule, "body", Rule);

        Assert.Throws<ProjectionException>(() => LayoutSnapshot.Render(rule, notations, width: 40));
    }

    private static string Render(Func<NotationEditor, BlockNotationCell> build, bool enabled)
    {
        var concepts = Concepts();
        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, build);

        var model = new Project(concepts).NewModel();
        var rule = model.NewNode(Rule);

        rule.Set("enabled", enabled ? "true" : "false");

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
                new PropertySlot("enabled", PropertyType.Boolean, "true"),
                new PropertySlot("name", PropertyType.String),
                new ChildSlot("body", Rule, Cardinality.Many),
            ]),
    ]);
}
