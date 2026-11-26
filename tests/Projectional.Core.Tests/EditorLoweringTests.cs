using Projectional.Core;
using Projectional.Editing;
using Projectional.Editors;
using Projectional.Editors.Rendering;
using Projectional.Language;
using Projectional.Persistence;
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

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        rule.Set("name", "Approved");

        Assert.Equal("rule Approved", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ADeclaredTypeOfDrawsTheNodesType()
    {
        // The declared path for the annotation: an `@type@` cell in the editor language becomes the same
        // `TypeOfNotationCell` a C# notation spells `ed.TypeOf()`, so a language written as a model can show its types
        // too. No registry is passed here, so the answer is `unknown` - which is also the proof that the cell draws
        // what the registry says rather than a fixed string.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Rules.Rule", [new PropertySlot("name", PropertyType.String)]),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);
        var line = notation.Line(root, EditorConceptKeys.Property, EditorConceptKeys.Space);

        line.GetChildren(EditorLanguage.CellSlot)[0].Set("slot", "name");
        notation.Cell(line, EditorConceptKeys.TypeOf);

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        rule.Set("name", "Approved");

        Assert.Equal("Approved unknown", LayoutSnapshot.Render(rule, notations, width: 40));
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

        var error = Assert.Throws<ConceptDefinitionException>(() => EditorLowering.Lower(notation.Roots, concepts));

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

        Assert.Throws<NotationException>(() => EditorLowering.CreateRegistry(notation.Roots, concepts));
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

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
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

        return EditorLowering.CreateRegistry(notation.Roots, concepts);
    }

    [Fact]
    public void ADeclaredWhileFilledDrawsItsCellsOnlyWhileTheSlotHoldsAChild()
    {
        // The twin of the conditional, one level over: a slot instead of a property. It has to exist as a cell
        // concept for the same reason - a notation that can be written in C# and not as data would make the two
        // authoring paths different languages (ADR 0014).
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Rules.Rule",
                [
                    new ChildSlot("body", "Rules.Rule", Cardinality.Many),
                ]),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);
        var line = notation.Line(root, EditorConceptKeys.WhileFilled, EditorConceptKeys.Keyword);
        var cells = line.GetChildren(EditorLanguage.CellSlot);

        cells[0].Set("slot", "body");
        cells[1].Set("text", "rule");

        // The space belongs to the cell, like the conditional's does: outside it, the blank would stay behind in
        // every drawing of a concept that has no body.
        notation.Cell(cells[0], EditorConceptKeys.Keyword, ("text", "has"));
        notation.Cell(cells[0], EditorConceptKeys.Space);

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        Assert.Equal("rule", LayoutSnapshot.Render(rule, notations, width: 40));

        model.NewChild(rule, "body", "Rules.Rule");

        Assert.Equal("has rule", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ADeclaredWhileFilledIsValidatedByTheRegistry()
    {
        // Same division as the conditional: a slot the concept does not declare is the notation layer's refusal,
        // because the declared cell lowers to the same cell the hand-written notation uses.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "Rules.Rule",
                [
                    new PropertySlot("enabled", PropertyType.Boolean, "true"),
                    new ChildSlot("body", "Rules.Rule", Cardinality.Many),
                ]),
        ]);

        Assert.Throws<NotationException>(() => WhileFilled(concepts, "enabled"));
        Assert.Throws<NotationException>(() => WhileFilled(concepts, "nope"));
    }

    [Fact]
    public void ADeclaredWhileFilledBlockLeavesNoRowWhileTheSlotIsEmpty()
    {
        // The block sibling of the inline cell, and the reason it is a separate concept: an empty block would still
        // occupy a row, so "draw nothing" has to remove the group from the tree instead of emptying it.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "C.Rule",
                [
                    new ChildSlot("body", "C.Body", Cardinality.One),
                    new ChildSlot("otherwise", "C.Body", Cardinality.One),
                ]),
            new ConceptDeclaration("C.Body", [new PropertySlot("name", PropertyType.String)], nameSlot: "name"),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);

        notation.Cell(notation.Line(root), EditorConceptKeys.ChildInline, ("slot", "body"));

        var branch = notation.Cell(root, EditorConceptKeys.WhileFilledBlock, ("slot", "otherwise"));

        notation.Cell(notation.Line(branch), EditorConceptKeys.Keyword, ("text", "else"));
        notation.Cell(notation.Line(branch), EditorConceptKeys.ChildInline, ("slot", "otherwise"));

        var body = notation.Notation("C.Body");
        var bodyLine = notation.SetRoot(body, EditorConceptKeys.Line);

        notation.Slot(bodyLine, EditorConceptKeys.Property, "name");

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("C.Rule");

        model.NewChild(rule, "body", "C.Body").Set("name", "then");

        // Not an empty line where the branch would be - nothing at all.
        Assert.Equal("then", LayoutSnapshot.Render(rule, notations, width: 40));

        model.NewChild(rule, "otherwise", "C.Body").Set("name", "otherwise");

        Assert.Equal("then\nelse\notherwise", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ADeclaredWhileFilledBlockIsValidatedByTheRegistry()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("C.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);

        notation.Cell(root, EditorConceptKeys.WhileFilledBlock, ("slot", "missing"));

        Assert.Throws<NotationException>(() => EditorLowering.CreateRegistry(notation.Roots, concepts));
    }

    [Fact]
    public void ADeclaredConditionalBlockLeavesNoRowWhileThePropertyDoesNotHold()
    {
        // The declared twin of the runtime cell, and the pair a language needs to draw one concept in two shapes:
        // C# 10's file-scoped namespace is one concept with one boolean, drawn with a semicolon or with braces.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "C.Rule",
                [new PropertySlot("kind", PropertyType.String, "block")]),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);

        notation.Cell(notation.Line(root), EditorConceptKeys.Property, ("slot", "kind"));

        var scoped = notation.Cell(root, EditorConceptKeys.ConditionalBlock, ("slot", "kind"), ("value", "scoped"));
        var block = notation.Cell(root, EditorConceptKeys.ConditionalBlock, ("slot", "kind"), ("value", "block"));

        notation.Cell(notation.Line(scoped), EditorConceptKeys.Keyword, ("text", "scoped"));
        notation.Cell(notation.Line(block), EditorConceptKeys.Keyword, ("text", "block"));

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("C.Rule");

        rule.Set("kind", "scoped");

        Assert.Equal("scoped\nscoped", LayoutSnapshot.Render(rule, notations, width: 40));

        rule.Set("kind", "block");

        Assert.Equal("block\nblock", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void ADeclaredConditionalBlockIsValidatedByTheRegistry()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "C.Rule",
                [new PropertySlot("kind", PropertyType.Boolean, "false")]),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);

        notation.Cell(root, EditorConceptKeys.ConditionalBlock, ("slot", "kind"), ("value", "maybe"));

        // A value the type would refuse can never hold, so the group could never be drawn.
        Assert.Throws<NotationException>(() => EditorLowering.CreateRegistry(notation.Roots, concepts));
    }

    [Fact]
    public void ADeclaredImageDrawsAPictureFromItsKeyAndBox()
    {
        // The declared path and the hand-written one must produce the same cell, or a language written as a project
        // would be a weaker kind of language. `width`/`height` left out mean "as tall as the line", which is what the
        // notation's `line` placeholder says.
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("C.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.Image, ("key", "key"));

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("C.Rule");

        var tree = new EditorTreeBuilder(notations).Build(rule);
        var image = Assert.IsType<ImageCell>(tree.Root.Children[0]);

        Assert.Equal("key", image.Key);
        Assert.Equal(0, image.Width);
        Assert.Equal(0, image.Height);

        // A picture draws no text, so the text grid shows its room and no glyph.
        Assert.Equal(string.Empty, CellText.Of(image));
        Assert.Equal(string.Empty, LayoutSnapshot.Render(rule, notations, width: 20).Trim());
    }

    [Fact]
    public void ADeclaredImageKeepsTheBoxItsAuthorWrote()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("C.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.Image, ("key", "logo"), ("width", "6"), ("height", "3"));

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("C.Rule");

        model.AddRoot(rule);

        var layout = new CellLayouter(MonospaceTextMeasure.Instance).Layout(
            new EditorTreeBuilder(notations).Build(rule));

        // The box is the one the author wrote, and layout keeps it rather than measuring the artwork - which is the
        // whole reason a picture needs no bitmap seam in the projection layer.
        var image = Assert.Single(layout.Cells, placed => placed.Cell is ImageCell);

        Assert.Equal(6, image.Bounds.Width);
        Assert.Equal(3, image.Bounds.Height);
        Assert.Equal("logo", Assert.IsType<ImageCell>(image.Cell).Key);
    }

    [Fact]
    public void ADeclaredImageWithoutAKeyIsRefused()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("C.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.Image);

        var error = Assert.Throws<ConceptDefinitionException>(
            () => EditorLowering.CreateRegistry(notation.Roots, concepts));

        Assert.Contains("'key'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredImageWithANegativeBoxIsRefusedRatherThanLayouted()
    {
        // Refused while the notation is lowered, so the message names the picture and the slot. Left to the cell it
        // would throw for a parameter name the author has never seen.
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("C.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.Image, ("key", "logo"), ("width", "-2"));

        var error = Assert.Throws<ConceptDefinitionException>(
            () => EditorLowering.CreateRegistry(notation.Roots, concepts));

        Assert.Contains("'width'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredImageSurvivesASaveAndLoad()
    {
        // The cell is stored like any other editor cell, so a language written as a project keeps its pictures through
        // a save. Pinned here rather than left to the corpus tests because those round-trip the samples they happen to
        // hold, which is not the same as knowing a new cell persists.
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("C.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.Image, ("key", "logo"), ("width", "6"), ("height", "3"));

        var document = notation.Documents[0];
        var stored = ModelXml.Serialize(document);

        Assert.Equal(stored, ModelXml.Serialize(ModelXml.Deserialize(EditorLanguage.NewProject(), stored)));

        var reloaded = ModelXml.Deserialize(EditorLanguage.NewProject(), stored);
        var notations = EditorLowering.CreateRegistry(reloaded.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var image = Assert.IsType<ImageCell>(
            new EditorTreeBuilder(notations).Build(model.NewNode("C.Rule")).Root.Children[0]);

        Assert.Equal("logo", image.Key);
        Assert.Equal(6, image.Width);
        Assert.Equal(3, image.Height);
    }

    /// <summary>A language with one concept holding a list of arguments, which is what a call looks like.</summary>
    private static ConceptRegistry CallConcepts() =>
        ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "C.Call",
                [
                    new ChildSlot("arguments", "C.Argument", Cardinality.Many),
                ]),
            new ConceptDeclaration("C.Argument", [new PropertySlot("name", PropertyType.String)], nameSlot: "name"),
        ]);

    /// <summary>
    /// The notation of <c>C.Call</c> as <c>(list)</c>, with the list either drawn always or only while it holds
    /// something.
    /// </summary>
    private static NotationRegistry CallNotation(ConceptRegistry concepts, bool hideEmptyList)
    {
        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Call");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.Punct, ("text", "("));

        var owner = hideEmptyList
            ? notation.Cell(line, EditorConceptKeys.WhileFilled, ("slot", "arguments"))
            : line;

        var list = notation.Cell(owner, EditorConceptKeys.InlineCollection, ("slot", "arguments"), ("emptyText", ""));

        notation.Cell(line, EditorConceptKeys.Punct, ("text", ")"));
        notation.Separator(list, ",");

        var argument = notation.Notation("C.Argument");
        var argumentLine = notation.SetRoot(argument, EditorConceptKeys.Line);

        notation.Slot(argumentLine, EditorConceptKeys.Property, "name");

        return EditorLowering.CreateRegistry(notation.Roots, concepts);
    }

    /// <summary>A one-line notation whose only cell is a "while filled" cell over <paramref name="slot"/>.</summary>
    private static NotationRegistry WhileFilled(ConceptRegistry concepts, string slot)
    {
        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Sequence);
        var cell = notation.Line(root, EditorConceptKeys.WhileFilled).GetChildren(EditorLanguage.CellSlot)[0];

        cell.Set("slot", slot);

        return EditorLowering.CreateRegistry(notation.Roots, concepts);
    }

    [Fact]
    public void ADeclaredInlineCollectionKeepsItsListOnTheLine()
    {
        // The block collection cannot be used for a call's arguments: it would put every argument on a line of its
        // own. This is the cell that says `f(a, b)`, and it is declared as an inline cell so a notation that puts it
        // in a sequence fails to lower instead of laying out wrongly.
        var concepts = CallConcepts();
        var notations = CallNotation(concepts, hideEmptyList: false);
        var model = new Project(concepts).NewModel();
        var call = model.NewNode("C.Call");

        // An empty slot keeps one space, because a slot the user has to fill has to be a place the caret can stand on
        // (see CellLayouter.SingleLineSize). An author who wants `f()` rather than `f( )` puts the list in a
        // while-filled cell, which is the next test.
        Assert.Equal("( )", LayoutSnapshot.Render(call, notations, width: 40));

        model.NewChild(call, "arguments", "C.Argument").Set("name", "a");
        model.NewChild(call, "arguments", "C.Argument").Set("name", "b");

        Assert.Equal("(a, b)", LayoutSnapshot.Render(call, notations, width: 40));
    }

    [Fact]
    public void ADeclaredInlineCollectionInAWhileFilledCellDrawsNothingWhileTheSlotIsEmpty()
    {
        // The pair a language needs for `f()`: the while-filled cell is what makes an optional list disappear, and it
        // is inline, so it can wrap a list that stays on the line.
        var concepts = CallConcepts();
        var notations = CallNotation(concepts, hideEmptyList: true);
        var model = new Project(concepts).NewModel();
        var call = model.NewNode("C.Call");

        Assert.Equal("()", LayoutSnapshot.Render(call, notations, width: 40));

        model.NewChild(call, "arguments", "C.Argument").Set("name", "a");
        model.NewChild(call, "arguments", "C.Argument").Set("name", "b");

        Assert.Equal("(a, b)", LayoutSnapshot.Render(call, notations, width: 40));
    }

    [Fact]
    public void ADeclaredInlineCollectionIsValidatedByTheRegistry()
    {
        // Lowered through the same builder as the hand-written cell, so the slot check is the notation layer's.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                "C.Call",
                [
                    new ChildSlot("arguments", "C.Argument", Cardinality.Many),
                ]),
            new ConceptDeclaration("C.Argument", [new PropertySlot("name", PropertyType.String)], nameSlot: "name"),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Call");
        var line = notation.SetRoot(declared, EditorConceptKeys.Line);

        notation.Cell(line, EditorConceptKeys.InlineCollection, ("slot", "missing"));

        Assert.Throws<NotationException>(() => EditorLowering.CreateRegistry(notation.Roots, concepts));
    }

    [Fact]
    public void ADeclaredHorizontalPutsItsBlocksInOneRow()
    {
        // The twin of the C# cell, so the declared path can say "beside each other" too. It becomes the same
        // HorizontalNotationCell, which is the whole point of lowering through the builder.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("C.Rule", [new PropertySlot("name", PropertyType.String)]),
        ]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("C.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Horizontal);

        var left = notation.Cell(root, EditorConceptKeys.Line);
        var right = notation.Cell(root, EditorConceptKeys.Line);

        notation.Cell(left, EditorConceptKeys.Keyword, ("text", "x"));
        notation.Cell(right, EditorConceptKeys.Keyword, ("text", "y"));

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();

        Assert.Equal("xy", LayoutSnapshot.Render(model.NewNode("C.Rule"), notations, width: 40).Trim());
    }

    [Fact]
    public void AVariantTravelsWithTheNotation()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("Rules.Rule")]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule", variant: "table");

        notation.SetRoot(declared, EditorConceptKeys.Sequence);

        var lowered = Assert.Single(EditorLowering.Lower(notation.Roots, concepts));

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

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        Assert.Equal("<no rules>", LayoutSnapshot.Render(rule, notations, width: 40));
    }

    [Fact]
    public void APropertyCellKeepsTheEmptyTextTheAuthorWrote()
    {
        // The stand-in an empty property shows is notation data like the placeholder of a child slot, so a
        // language written in project files can say it too - and the rendering proves it reaches the drawing.
        var concepts = ConceptRegistry.Create(
            [new ConceptDeclaration("Rules.Rule", [new PropertySlot("operator", PropertyType.Enum("+", "-"))])]);

        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var sequence = notation.Sequence(notation.SetRoot(declared, EditorConceptKeys.Sequence));
        var line = notation.Line(sequence);

        notation.Cell(line, EditorConceptKeys.Property, ("slot", "operator"), ("emptyText", "<<operator>>"));

        var notations = EditorLowering.CreateRegistry(notation.Roots, concepts);
        var model = new Project(concepts).NewModel();
        var rule = model.NewNode("Rules.Rule");

        model.AddRoot(rule);

        Assert.Equal("<<operator>>", LayoutSnapshot.Render(rule, notations, width: 40));
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
        line.GetChildren(EditorLanguage.CellSlot)[2].Set("emptyText", "<<name>>");

        Assert.Equal(
            string.Join(
                '\n',
                "  editor for concept Rules.Rule variant: default",
                "      node cell layout:",
                "      [/",
                "        [-rule- ; %name% empty: <<name>>]",
                "      /]"),
            LayoutSnapshot.Render(notation.Root, EditorNotations.Create(), width: 120));
    }

    [Fact]
    public void ATypeOfCellInADefinitionIsDrawnButTheCaretStepsOverIt()
    {
        // `@type@` in an editor model says "the type is computed", so it is an annotation and not a word the author
        // types. The drawing still shows it - it is part of how the notation reads - but the arrow keys walk the
        // cells the author actually fills in, which is the property beside it.
        var notation = new NotationBuilder();
        var declared = notation.Notation("Rules.Rule");
        var root = notation.SetRoot(declared, EditorConceptKeys.Line);
        var property = notation.Cell(root, EditorConceptKeys.Property);

        property.Set("slot", "name");
        notation.Cell(root, EditorConceptKeys.Space);
        notation.Cell(root, EditorConceptKeys.TypeOf);

        var tree = new EditorTreeBuilder(EditorNotations.Create()).Build(notation.Root);
        var places = CaretPlaces.Of(tree);
        var typeOf = Assert.Single(tree.Cells().OfType<ConstantCell>(), cell => cell.Text == "@type@");

        // Drawn, and skipped: the cell is in the tree and not in the list the arrow keys walk.
        Assert.DoesNotContain(places, place => place.Cell == typeOf);
        Assert.Contains(places, place => place.Cell is PropertyCell);

        Assert.Contains(tree.Cells().OfType<ConstantCell>(), cell => cell.Text == "@type@");
        Assert.DoesNotContain(places, place => place.Cell is ConstantCell { Kind: ConstantKind.Annotation });
        Assert.Contains(places, place => place.Cell is PropertyCell);
    }

    [Fact]
    public void ADocumentThatIsNotANotationIsRefused()
    {
        var concepts = ConceptRegistry.Create([new ConceptDeclaration("Rules.Rule")]);
        var model = EditorLanguage.NewModel();
        var cell = model.NewNode(EditorConceptKeys.Sequence);

        model.AddRoot(cell);

        var error = Assert.Throws<ConceptDefinitionException>(() => EditorLowering.Lower([cell], concepts));

        Assert.Contains("is not a notation", error.Message, StringComparison.Ordinal);
    }
}
