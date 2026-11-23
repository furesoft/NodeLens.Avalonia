using Projectional.Behaviours;
using Projectional.Core;
using Projectional.Projection;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The behaviour language's own notation: a document draws as the definition it is.</summary>
public sealed class BehaviourNotationTests
{
    [Fact]
    public void ADocumentDrawsItsHeaderAndItsFunction()
    {
        var model = BehaviourLanguage.NewModel();
        var document = model.Roots[0];

        document.Set(BehaviourConceptKeys.ConceptSlot, "CSharp.Field");

        var function = Function(model, document);
        Body(model, function, BehaviourConceptKeys.Property, "name");

        Assert.Equal(
            """
              behaviour for CSharp.Field
                name: property name
            """,
            LayoutSnapshot.Render(model, BehaviourNotations.Registry, width: 60));
    }

    [Fact]
    public void AJoinDrawsItsPartsOnOneLineWithTheSeparatorBetweenThem()
    {
        var model = BehaviourLanguage.NewModel();
        var function = Function(model, model.Roots[0]);
        var join = Body(model, function, BehaviourConceptKeys.Join, read: null);

        join.Set(BehaviourConceptKeys.SeparatorSlot, ".");
        LiteralPart(model, join, BehaviourConceptKeys.PartsSlot, "at");
        Part(model, join, BehaviourConceptKeys.PartsSlot, BehaviourConceptKeys.Child, "type");

        Assert.Equal(
            """
              behaviour for
                name: join . (literal at, child type)
            """,
            LayoutSnapshot.Render(model, BehaviourNotations.Registry, width: 60));
    }

    [Fact]
    public void AChoiceDrawsBothArms()
    {
        var model = BehaviourLanguage.NewModel();
        var function = Function(model, model.Roots[0]);
        var choose = Body(model, function, BehaviourConceptKeys.Choose, read: null);

        choose.Set(BehaviourConceptKeys.SlotSlot, "isPostfix");
        choose.Set(BehaviourConceptKeys.ValueSlot, "true");
        LiteralPart(model, choose, BehaviourConceptKeys.ThenSlot, "suffix");
        LiteralPart(model, choose, BehaviourConceptKeys.OtherwiseSlot, "prefix");

        Assert.Equal(
            """
              behaviour for
                name: choose isPostfix = true ? literal suffix : literal prefix
            """,
            LayoutSnapshot.Render(model, BehaviourNotations.Registry, width: 100));
    }

    [Fact]
    public void EveryConcreteConceptCanBeDrawnAndTheDocumentIsTheOnlyRoot()
    {
        foreach (var concept in BehaviourLanguage.Concepts.Concepts.Where(concept => !concept.IsAbstract))
        {
            Assert.True(BehaviourNotations.Registry.TryGet(concept.Key, out _), concept.Key);
        }

        // A part is always one of the kinds below it, so it is never a node of its own.
        Assert.True(BehaviourLanguage.Concepts.Get(BehaviourConceptKeys.Part).IsAbstract);

        // One root, and it is the document: the framework's own concepts are injected into every registry and are not
        // this language's answer.
        Assert.Equal(
            [BehaviourConceptKeys.Document],
            BehaviourLanguage.Concepts.Concepts
                .Where(concept => concept.IsRoot && concept.Key.StartsWith("Behaviour.", StringComparison.Ordinal))
                .Select(concept => concept.Key));
    }

    [Fact]
    public void AResolveInfoPartDrawsItsSlot()
    {
        var model = BehaviourLanguage.NewModel();
        var function = Function(model, model.Roots[0]);

        Body(model, function, BehaviourConceptKeys.ResolveInfo, "to");

        Assert.Equal(
            """
              behaviour for
                name: resolveInfo to
            """,
            LayoutSnapshot.Render(model, BehaviourNotations.Registry, width: 60));
    }

    [Fact]
    public void AChildrenPartDrawsTheSlotAndTheSeparator()
    {
        var model = BehaviourLanguage.NewModel();
        var function = Function(model, model.Roots[0]);
        var children = Body(model, function, BehaviourConceptKeys.Children, "items");

        children.Set(BehaviourConceptKeys.SeparatorSlot, ",");

        Assert.Equal(
            """
              behaviour for
                name: children items with ,
            """,
            LayoutSnapshot.Render(model, BehaviourNotations.Registry, width: 60));
    }

    private static Node Function(Model model, Node document)    {
        var function = model.NewChild(document, BehaviourConceptKeys.FunctionsSlot, BehaviourConceptKeys.Function);
        function.Set(BehaviourConceptKeys.KindSlot, BehaviourConceptKeys.NameKind);

        return function;
    }

    private static Node Body(Model model, Node function, string conceptKey, string? read) =>
        Part(model, function, BehaviourConceptKeys.BodySlot, conceptKey, read);

    private static Node Part(Model model, Node owner, string slot, string conceptKey, string? read)
    {
        var part = model.NewChild(owner, slot, conceptKey);

        if (read is not null)
        {
            part.Set(BehaviourConceptKeys.SlotSlot, read);
        }

        return part;
    }

    private static Node LiteralPart(Model model, Node owner, string slot, string text)
    {
        var part = model.NewChild(owner, slot, BehaviourConceptKeys.Literal);
        part.Set(BehaviourConceptKeys.TextSlot, text);

        return part;
    }
}
