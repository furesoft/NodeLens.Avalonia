using Projectional.Core;
using Projectional.Editors;
using Projectional.Editors.Rendering;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A notation written as a model, lowered into the same <see cref="Notation"/> objects a language author writes by
/// hand.
/// </summary>
/// <remarks>
/// The tests go through the outside of the feature: declare a notation, lower it, and draw a node of the language
/// it is about. What a declared notation can and cannot say is the interesting part, so the refusals are tested as
/// carefully as the success.
/// </remarks>
public sealed class EditorLoweringTests
{
    [Fact]
    public void ADeclaredNotationDrawsTheConceptItIsAbout()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Rules.Rule",
                [
                    new PropertySlot("name", PropertyType.String),
                    new ChildSlot("body", "Rules.Body", Cardinality.One),
                ],
                nameSlot: "name"),
            new ConceptDeclaration("Rules.Body", [new PropertySlot("text", PropertyType.String)]),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);

        var line = notation.Line(
            root,
            EditorConceptKeys.Keyword,
            EditorConceptKeys.Space,
            EditorConceptKeys.Property);

        // The keyword carries its text, the property names its slot, and the gap between them is a cell.
        line.GetChildren(EditorLanguage.CellSlot)[0].Set("text", "rule");
        line.GetChildren(EditorLanguage.CellSlot)[2].Set("slot", "name");

        var notations = EditorLowering.CreateRegistry(notation.Root, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        rule.Set("name", "Approved");

        Assert.Equal("rule Approved", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ABlockInsideALineCannotBeWritten()
    {
        // The rule the C# builder expresses with its type hierarchy - `ed.Line(ed.Indent(...))` does not compile -
        // is carried by the slot declarations instead, so the model refuses it before lowering ever runs.
        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Line);
        // The insert itself is what the model refuses, so the attempt is the assertion.
        Assert.ThrowsAny<ModelInvariantException>(
            () => notation.Cell(root, EditorConceptKeys.Sequence));
    }

    [Fact]
    public void ANotationAboutAnUndeclaredConceptIsRefused()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("Rules.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Missing");

        notation.SetRoot(declared, EditorConceptKeys.Sequence);

        var error = Assert.Throws<ConceptDefinitionException>(() => EditorLowering.Lower(notation.Root, concepts));

        Assert.Contains("Rules.Missing", error.Message, StringComparison.Ordinal);
        Assert.Contains("does not declare", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANotationNamingASlotThatDoesNotExistIsRefusedByTheRegistry()
    {
        // The projection layer's own rule, not a second copy of it: a property cell whose slot the concept does
        // not declare is what `NotationRegistry.Add` has always refused.
        var concepts = ConceptRegistry.Create(
            [new ConceptDeclaration("Rules.Rule", [new PropertySlot("name", PropertyType.String)])]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);
        var line = notation.Line(root, EditorConceptKeys.Property);

        line.GetChildren(EditorLanguage.CellSlot)[0].Set("slot", "nope");

        Assert.Throws<NotationException>(() => EditorLowering.CreateRegistry(notation.Root, concepts));
    }

    [Fact]
    public void ADeclaredConditionalDrawsItsCellsOnlyWhileThePropertyHolds()
    {
        // The declared path has to be able to say everything C# can, so a conditional is a cell concept like any
        // other - and it becomes the same ConditionalNotationCell the hand-written notation uses.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Rules.Rule",
                [
                    new PropertySlot("enabled", PropertyType.Boolean, "true"),
                    new PropertySlot("name", PropertyType.String),
                ],
                nameSlot: "name"),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);
        var line = notation.Line(root, EditorConceptKeys.Conditional, EditorConceptKeys.Keyword);
        var cells = line.GetChildren(EditorLanguage.CellSlot);

        cells[0].Set("slot", "enabled");
        cells[0].Set("value", "true");
        cells[1].Set("text", "rule");

        // The space belongs to the conditional, not to the line: a gap outside it would stay behind as a leading
        // blank in every drawing of a concrete concept.
        notation.Cell(cells[0], EditorConceptKeys.Keyword, ("text", "on"));
        notation.Cell(cells[0], EditorConceptKeys.Space);

        var notations = EditorLowering.CreateRegistry(notation.Root, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        Assert.Equal("on rule", LayoutSnapshot.Render(rule, notations, width: 40));

        rule.Set("enabled", "false");

        Assert.Equal("rule", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ADeclaredConditionalIsValidatedByTheRegistry()
    {
        // The lowering adds no rule of its own here: a condition the notation layer would refuse is refused for a
        // declared notation too, because it becomes the same cell.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Rules.Rule",
                [
                    new PropertySlot("enabled", PropertyType.Boolean, "true"),
                    new ChildSlot("body", "Rules.Rule", Cardinality.Many),
                ]),
        ]);

        Assert.Throws<NotationException>(() => Conditioned(concepts, "body", "true"));
        Assert.Throws<NotationException>(() => Conditioned(concepts, "enabled", "yes"));
    }

    /// <summary>A one-line notation whose only cell is a conditional on <paramref name="slot"/>.</summary>
    private static NotationRegistry Conditioned(ConceptRegistry concepts, string slot, string value)
    {
        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);
        var cell = notation.Line(root, EditorConceptKeys.Conditional).GetChildren(EditorLanguage.CellSlot)[0];

        cell.Set("slot", slot);
        cell.Set("value", value);

        return EditorLowering.CreateRegistry(notation.Root, concepts);
    }

    [Fact]
    public void AVariantTravelsWithTheNotation()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("Rules.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule", variant: "table");

        notation.SetRoot(declared, EditorConceptKeys.Sequence);

        var lowered = Assert.Single(EditorLowering.Lower(notation.Root, concepts));

        Assert.Equal("table", lowered.Variant);
    }

    [Fact]
    public void ANamedSlotKeepsTheEmptyTextTheAuthorWrote()
    {
        var concepts = ConceptRegistry.Create(
            [new ConceptDeclaration("Rules.Rule", [new ChildSlot("body", "Rules.Rule", Cardinality.Many)])]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.Sequence(notation.SetRoot(declared, EditorConceptKeys.Sequence));

        notation.Cell(root, EditorConceptKeys.Children, ("slot", "body"), ("emptyText", "<no rules>"));

        var notations = EditorLowering.CreateRegistry(notation.Root, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        Assert.Equal("<no rules>", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ADefinitionOfANotationIsDrawnLikeAnyOtherModel()
    {
        // The plan's rule: a definition language that needed a rendering exception would not be a language. The
        // brackets are the reading aid - `[/ ... /]` stacks, `[>` is one cell of a line.
        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);
        var line = notation.Line(root, EditorConceptKeys.Keyword, EditorConceptKeys.Space, EditorConceptKeys.Property);

        line.GetChildren(EditorLanguage.CellSlot)[0].Set("text", "rule");
        line.GetChildren(EditorLanguage.CellSlot)[2].Set("slot", "name");

        Assert.Equal(
            string.Join(
                '\n',
                "notations:",
                "  editor for concept Rules.Rule variant: default",
                "  node cell layout:",
                "    [/",
                "      [> line",
                "        [> constant \"rule\" [> space [> {name}",
                "    /]"),
            LayoutSnapshot.Render(notation.Root, EditorNotations.Create(), width: 120));
    }

    [Fact]
    public void ADocumentThatIsNotANotationIsRefused()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("Rules.Rule")]);
        var model = EditorLanguage.NewModel();
        var cell = model.NewNode(EditorConceptKeys.Sequence);

        model.AddRoot(cell);

        var error = Assert.Throws<ConceptDefinitionException>(() => EditorLowering.Lower(cell, concepts));

        Assert.Contains("is not a notation", error.Message, StringComparison.Ordinal);
    }
}
