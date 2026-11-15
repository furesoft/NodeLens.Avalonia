using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Notation is validated once, when it is registered, so that the projection engine never has to
/// wonder whether a cell names a slot that actually exists.
/// </summary>
public sealed class NotationRegistryTests
{
    [Fact]
    public void NotationsAreKeptInRegistrationOrder() => Assert.Equal(
        [TestLanguage.Rule, TestLanguage.Body, TestLanguage.Step, TestLanguage.Note],
        TestNotations.Registry.Notations.Select(notation => notation.ConceptKey));

    [Fact]
    public void AConceptWithItsOwnNotationGetsIt()
    {
        var rule = TestLanguage.Registry.Get(TestLanguage.Rule);

        Assert.Same(TestNotations.Registry.Get(TestLanguage.Rule), TestNotations.Registry.FindFor(rule));
    }

    [Fact]
    public void AConceptWithoutItsOwnNotationInheritsTheNearestOne()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Base", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Test.Middle", superConcepts: ["Test.Base"]),
            new ConceptDeclaration("Test.Leaf", superConcepts: ["Test.Middle"]),
        ]);
        var notations = new NotationRegistry(concepts);
        var baseNotation = notations.Add("Test.Base", ed => ed.Line(ed.Property("name")));

        Assert.Same(baseNotation, notations.FindFor(concepts.Get("Test.Leaf")));
    }

    [Fact]
    public void AConceptWithNoNotationAnywhereInItsAncestryHasNone() =>
        Assert.Null(TestNotations.Registry.FindFor(TestLanguage.Registry.Get(TestLanguage.Tag)));

    [Fact]
    public void AskingForANotationNobodyDeclaredThrows() =>
        Assert.Throws<KeyNotFoundException>(() => TestNotations.Registry.Get(TestLanguage.Tag));

    [Fact]
    public void ALineKeepsItsInlineCellsInOrder()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var notation = notations.Add(
            TestLanguage.Note,
            ed => ed.Line(ed.Keyword("note"), ed.Space, ed.Property("text")));

        var line = Assert.IsType<LineNotationCell>(notation.Root);
        Assert.Collection(
            line.Content,
            cell =>
            {
                var constant = Assert.IsType<ConstantNotationCell>(cell);
                Assert.Equal("note", constant.Text);
                Assert.Equal(ConstantKind.Keyword, constant.Kind);
            },
            cell => Assert.IsType<SpaceNotationCell>(cell),
            cell => Assert.Equal("text", Assert.IsType<PropertyNotationCell>(cell).Slot));
    }

    [Fact]
    public void ACollectionCarriesItsSeparator()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var notation = notations.Add(TestLanguage.Body, ed => ed.Collection("notes", separator: ed.Punct(";")));

        var collection = Assert.IsType<CollectionNotationCell>(notation.Root);
        Assert.Equal("notes", collection.Slot);
        Assert.Equal(";", Assert.IsType<ConstantNotationCell>(collection.Separator).Text);
    }

    [Fact]
    public void GivingAConceptTheSameVariantTwiceIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);
        notations.Add(TestLanguage.Note, ed => ed.Line(ed.Property("text")));

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Note, ed => ed.Line(ed.Property("text"))));

        Assert.Contains("'Test.Note'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("already has a 'default' notation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANotationForAConceptThatDoesNotExistIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add("Test.Nope", ed => ed.Line(ed.Keyword("nope"))));

        Assert.Contains("'Test.Nope'", exception.Message, StringComparison.Ordinal);
        Assert.Empty(notations.Notations);
    }

    [Fact]
    public void APropertyCellOnASlotTheConceptDoesNotHaveIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Note, ed => ed.Line(ed.Property("body"))));

        Assert.Contains("property 'body'", exception.Message, StringComparison.Ordinal);
        Assert.Empty(notations.Notations);
    }

    [Fact]
    public void APropertyCellOnAChildSlotIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Body, ed => ed.Line(ed.Property("steps"))));

        Assert.Contains("property 'steps'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChildCellOnAPropertySlotIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Rule, ed => ed.Children("name")));

        Assert.Contains("children of 'name'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReferenceCellOnASlotTheConceptDoesNotHaveIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Step, ed => ed.Line(ed.Reference("tag"))));

        Assert.Contains("reference 'tag'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawingAManyChildSlotInlineIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Body, ed => ed.Line(ed.ChildInline("steps"))));

        Assert.Contains("holds many children", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawingAOneChildSlotAsABlockIsAllowed()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        notations.Add(TestLanguage.Rule, ed => ed.Sequence(ed.Line(ed.Keyword("rule")), ed.Children("body")));

        Assert.Single(notations.Notations);
    }

    [Fact]
    public void ADanglingSlotIsFoundInsideASequence()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Rule, ed => ed.Sequence(
                ed.Line(ed.Keyword("rule")),
                ed.Children("siblings"))));

        Assert.Contains("'siblings'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADanglingSlotIsFoundDeepInsideAnIndent()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() =>
            notations.Add(TestLanguage.Rule, ed => ed.Sequence(
                ed.Line(ed.Keyword("rule")),
                ed.Indent(
                    ed.Line(ed.Keyword("body"), ed.Space, ed.ChildInline("body")),
                    ed.Children("siblings")))));

        Assert.Contains("'siblings'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CellsAreWalkedInDocumentOrder() => Assert.Equal(
        [
            "sequence(3)",
            "line(9)",
            "'rule'",
            "space",
            "property name",
            "space",
            "'as'",
            "space",
            "reference tag",
            "space",
            "'{'",
            "indent(2)",
            "line(1)",
            "child body",
            "line(3)",
            "'extends'",
            "space",
            "child extends",
            "line(1)",
            "'}'",
        ],
        TestNotations.Registry.Get(TestLanguage.Rule).SelfAndDescendants().Select(cell => cell.ToString()));

    [Fact]
    public void ANotationThatBuildsNoRootCellIsRejected()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        var exception = Assert.Throws<NotationException>(() => notations.Add(TestLanguage.Note, _ => null!));

        Assert.Contains("built no root cell", exception.Message, StringComparison.Ordinal);
    }
}
