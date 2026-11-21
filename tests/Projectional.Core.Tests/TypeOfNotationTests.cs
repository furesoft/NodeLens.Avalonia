using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The <c>typeof</c> cell draws the node's type as an annotation, and the notation decides where.</summary>
/// <remarks>
/// The type is read while the tree is built, so these assertions are about the tree and not about a renderer: a cell
/// whose text is the type, whose kind is <see cref="ConstantKind.Annotation"/> and whose style is
/// <see cref="TextStyleKind.Annotation"/> is what a palette then paints quietly.
/// </remarks>
public sealed class TypeOfNotationTests
{
    [Fact]
    public void TheCellCarriesTheTypeTheLanguageDeclares()
    {
        var cells = Build(Typed(PropertyType.Integer));

        var annotation = Assert.Single(cells.OfType<ConstantCell>(), cell => cell.Kind == ConstantKind.Annotation);

        Assert.Equal("int", annotation.Text);
        Assert.Equal(TextStyleKind.Annotation, CellStyler.Default.StyleOf(annotation));

        // Not editable by construction: the editable cell is `PropertyCell`, and this is a `ConstantCell`, so the
        // caret can never type into the annotation. That is a fact of the type, not of a flag.
    }

    [Fact]
    public void TheAnnotationIsDrawnButTheCaretStepsOverIt()
    {
        // A type hint is an inlay the projection computed, not text the author wrote, so it is drawn on the line
        // but is not a caret stop: the caret walks the value and the space beside it and never lands in the type.
        // Skipping it is a caret fact and not a projection one - the cell is still there, and still painted.
        var tree = BuildTree(Typed(PropertyType.Integer));

        var places = CaretPlaces.Of(tree);

        Assert.DoesNotContain(places, place => place.Cell is ConstantCell { Kind: ConstantKind.Annotation });
        Assert.Contains(places, place => place.Cell is PropertyCell);
        Assert.Contains(tree.Cells().OfType<ConstantCell>(), cell => cell.Kind == ConstantKind.Annotation);
    }

    [Fact]
    public void AKeywordIsStillACaretStop()
    {
        // Only annotations are skipped. A keyword is text the notation wrote into the document, so the caret still
        // steps through it - skipping every constant would take away the ability to walk the line.
        var keyword = new ConstantCell(new CellAddress(NodeId.New()), "if", ConstantKind.Keyword);

        Assert.True(keyword.IsCaretTarget);
    }

    [Fact]
    public void ALanguageWithoutTypeRulesDrawsUnknown()
    {
        // Not nothing: "no rule said what this is" is the answer, and hiding the cell would make a language being
        // written look like a language whose notation is broken.
        var cells = Build(TypeRegistry.Empty);

        var annotation = Assert.Single(cells.OfType<ConstantCell>(), cell => cell.Kind == ConstantKind.Annotation);

        Assert.Equal(UnknownType.Instance.Name, annotation.Text);
    }

    [Fact]
    public void AConceptThatDoesNotMentionTheCellDrawsNoType()
    {
        // The notation decides, which is the whole point: a concept whose editor was written before the aspect
        // existed shows no annotation at all rather than one the framework added behind the author's back.
        var concepts = ConceptRegistry.Create(
            [new ConceptDeclaration("T.Number", [new PropertySlot("value", PropertyType.Integer)])]);

        var notations = new NotationRegistry(concepts);
        notations.Add("T.Number", ed => ed.Line(ed.Property("value")));

        var model = new Project(concepts).NewModel("m");
        var root = model.NewNode("T.Number");
        model.AddRoot(root);

        var cells = new EditorTreeBuilder(notations, types: Typed(PropertyType.Integer)).Build(root).Cells();

        Assert.DoesNotContain(cells.OfType<ConstantCell>(), cell => cell.Kind == ConstantKind.Annotation);
    }

    /// <summary>The cells of a document whose one concept draws its value followed by its type.</summary>
    private static IReadOnlyCollection<EditorCell> Build(TypeRegistry types) => [.. BuildTree(types).Cells()];

    /// <summary>The tree of a document whose one concept draws its value followed by its type.</summary>
    private static EditorTree BuildTree(TypeRegistry types)
    {
        var concepts = ConceptRegistry.Create(
            [new ConceptDeclaration("T.Number", [new PropertySlot("value", PropertyType.Integer)])]);

        var notations = new NotationRegistry(concepts);
        notations.Add("T.Number", ed => ed.Line(ed.Property("value"), ed.Space, ed.TypeOf()));

        var model = new Project(concepts).NewModel("m");
        var root = model.NewNode("T.Number");
        root.Set("value", "42");
        model.AddRoot(root);

        return new EditorTreeBuilder(notations, types: types).Build(root);
    }

    /// <summary>A registry that types every node of the sample as <paramref name="type"/>.</summary>
    private static TypeRegistry Typed(PropertyType type) =>
        TypeRegistry.Empty.Add("T.Number", new Fixed(type));

    private sealed class Fixed(PropertyType type) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => PrimitiveType.Of(type);
    }
}
