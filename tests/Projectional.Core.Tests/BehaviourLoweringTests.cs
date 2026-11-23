using Projectional.Behaviours;
using Projectional.Core;
using Projectional.CSharp;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A behaviour written as a model answers what its body says, and is refused where it does not fit.</summary>
/// <remarks>
/// Two models per test, because there are two languages: the document is written in <see cref="BehaviourLanguage"/>
/// and the nodes whose slots its body names are made in <see cref="BehaviourTestLanguage"/>. What is asserted is the
/// lowered delegate, not a registry - installing it into a concept declaration is the caller's step.
/// </remarks>
public sealed class BehaviourLoweringTests
{
    [Fact]
    public void APropertyBodyAnswersTheValue()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "value");

        var names = Lower(document);
        var node = BehaviourTestLanguage.NewModel().NewNode(BehaviourTestLanguage.Node);

        node.Set(BehaviourTestLanguage.ValueSlot, "42");

        Assert.Equal("42", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void AChildBodyNamesTheChild()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Child, "inner");

        var names = Lower(document);
        var nodes = BehaviourTestLanguage.NewModel();
        var node = nodes.NewNode(BehaviourTestLanguage.Node);

        // A node without the child says nothing rather than falling back to its own name.
        Assert.Equal(string.Empty, names[BehaviourTestLanguage.Node](node));

        nodes.NewChild(node, BehaviourTestLanguage.InnerSlot, BehaviourTestLanguage.Leaf);

        Assert.Equal("leaf", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void AReferenceBodyNamesTheTarget()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Reference, "target");

        var names = Lower(document);
        var nodes = BehaviourTestLanguage.NewModel();
        var node = nodes.NewNode(BehaviourTestLanguage.Node);
        var target = nodes.NewNode(BehaviourTestLanguage.Node);

        target.Set(BehaviourTestLanguage.ValueSlot, "count");
        node.SetReference(BehaviourTestLanguage.TargetSlot, target);

        Assert.Equal("count", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void AReferenceBodyAnswersNothingWhileTheReferenceDoesNotResolve()
    {
        // The resolveInfo fallback is the reference cell's business; a name made of a target that is not there is empty.
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Reference, "target");

        var names = Lower(document);

        Assert.Equal(
            string.Empty,
            names[BehaviourTestLanguage.Node](BehaviourTestLanguage.NewModel().NewNode(BehaviourTestLanguage.Node)));
    }

    [Fact]
    public void AResolveInfoBodyAnswersTheTextTheReferenceStored()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.ResolveInfo, "target");

        var names = Lower(document);
        var nodes = BehaviourTestLanguage.NewModel();
        var node = nodes.NewNode(BehaviourTestLanguage.Node);

        // A target the project does not hold is exactly the document this part exists for.
        node.SetReference(BehaviourTestLanguage.TargetSlot, NodeId.New(), "peak");

        Assert.Equal("peak", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void AResolveInfoBodyOnAPropertyIsRefused()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.ResolveInfo, "value");

        Assert.Contains("is not a reference", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void AChildrenBodyJoinsTheNamesInAList()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        var children = Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Children, "items");
        children.Set(BehaviourConceptKeys.SeparatorSlot, " - ");

        var names = Lower(document);
        var nodes = BehaviourTestLanguage.NewModel();
        var node = nodes.NewNode(BehaviourTestLanguage.Node);

        nodes.NewChild(node, BehaviourTestLanguage.ItemsSlot, BehaviourTestLanguage.Leaf).Set(BehaviourTestLanguage.ValueSlot, "peak");
        nodes.NewChild(node, BehaviourTestLanguage.ItemsSlot, BehaviourTestLanguage.Leaf).Set(BehaviourTestLanguage.ValueSlot, "lake");

        // The third child has no name of its own yet, and a list leaves an empty answer out instead of adding a separator.
        nodes.NewChild(node, BehaviourTestLanguage.ItemsSlot, BehaviourTestLanguage.Leaf).Set(BehaviourTestLanguage.ValueSlot, string.Empty);

        Assert.Equal("peak - lake", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void AChildrenBodyOnAPropertyIsRefused()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Children, "value");

        Assert.Contains(
            "is not a child",
            Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void AJoinPutsItsSeparatorBetweenThePartsThatSaySomething()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        var join = Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Join, read: null);
        join.Set(BehaviourConceptKeys.SeparatorSlot, ".");
        LiteralPart(behaviour, join, BehaviourConceptKeys.PartsSlot, "x");
        Part(behaviour, join, BehaviourConceptKeys.PartsSlot, BehaviourConceptKeys.Child, "inner");
        Part(behaviour, join, BehaviourConceptKeys.PartsSlot, BehaviourConceptKeys.Property, "value");

        var names = Lower(document);
        var nodes = BehaviourTestLanguage.NewModel();
        var node = nodes.NewNode(BehaviourTestLanguage.Node);

        // The child is empty, so the join leaves it out instead of writing two separators in a row.
        Assert.Equal("x.seven", names[BehaviourTestLanguage.Node](node));

        nodes.NewChild(node, BehaviourTestLanguage.InnerSlot, BehaviourTestLanguage.Leaf);

        Assert.Equal("x.leaf.seven", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void AFallbackAnswersTheFirstPartThatSaysSomething()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        var fallback = Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Fallback, read: null);
        Part(behaviour, fallback, BehaviourConceptKeys.PartsSlot, BehaviourConceptKeys.Child, "inner");
        LiteralPart(behaviour, fallback, BehaviourConceptKeys.PartsSlot, "anonymous");

        var names = Lower(document);
        var nodes = BehaviourTestLanguage.NewModel();
        var node = nodes.NewNode(BehaviourTestLanguage.Node);

        Assert.Equal("anonymous", names[BehaviourTestLanguage.Node](node));

        nodes.NewChild(node, BehaviourTestLanguage.InnerSlot, BehaviourTestLanguage.Leaf);

        Assert.Equal("leaf", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void AChoiceAnswersByThePropertyItReads()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        var choose = Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Choose, read: null);
        choose.Set(BehaviourConceptKeys.SlotSlot, BehaviourTestLanguage.FlagSlot);
        choose.Set(BehaviourConceptKeys.ValueSlot, "true");
        LiteralPart(behaviour, choose, BehaviourConceptKeys.ThenSlot, "flagged");
        LiteralPart(behaviour, choose, BehaviourConceptKeys.OtherwiseSlot, "plain");

        var names = Lower(document);
        var node = BehaviourTestLanguage.NewModel().NewNode(BehaviourTestLanguage.Node);

        Assert.Equal("plain", names[BehaviourTestLanguage.Node](node));

        node.Set(BehaviourTestLanguage.FlagSlot, "true");

        Assert.Equal("flagged", names[BehaviourTestLanguage.Node](node));
    }

    [Fact]
    public void ARealLanguageCanNameItsOwnNode()
    {
        // The fixture proves the vocabulary; this proves the narrowing works against a language it was not written for.
        var (behaviour, document) = Document(CSharpConceptKeys.Class);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "name");

        var names = BehaviourLowering.Lower([document], CSharpLanguage.Concepts);

        var model = new Project(CSharpLanguage.Concepts).NewModel("csharp");
        var file = model.NewNode(CSharpConceptKeys.File);
        var ns = model.NewChild(file, CSharpConceptKeys.NamespaceSlot, CSharpConceptKeys.Namespace);
        var cls = model.NewChild(ns, CSharpConceptKeys.ClassesSlot, CSharpConceptKeys.Class);

        cls.Set(CSharpLanguage.NameSlot, "Order");

        Assert.Equal("Order", names[CSharpConceptKeys.Class](cls));
    }

    [Fact]
    public void AChoiceOnAValueThePropertyCannotHoldIsRefused()
    {
        // A value the type refuses would answer the otherwise arm for ever, with nothing visibly wrong in the document.
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Choice(behaviour, document, BehaviourTestLanguage.FlagSlot, "yes");

        Assert.Contains(
            "is not a value of 'bool'",
            Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void AChoiceOnAValueThePropertyWouldStoreDifferentlyIsRefused()
    {
        // `01` parses as a number, and `count` stores it as `1` - so comparing against `01` would never match.
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Choice(behaviour, document, BehaviourTestLanguage.CountSlot, "01");

        Assert.Contains(
            "stores it as '1'",
            Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void AChoiceWithoutAValueIsRefused()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Choice(behaviour, document, BehaviourTestLanguage.FlagSlot, value: null);

        Assert.Contains(
            "must name the value",
            Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void AChildPartOnAListIsRefused()
    {
        // The slot would answer its first element, which is a name the author did not ask for and cannot see.
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Child, "items");

        Assert.Contains(
            "holds a list of children",
            Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void AConceptThatDeclaresTwoNamesIsRefused()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "value");
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "value");

        Assert.Contains("more than one name", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void ADocumentThatNamesNoConceptIsRefused()
    {
        var (behaviour, document) = NewDocument();
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "value");

        Assert.Contains("must name the concept", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void ADocumentForAConceptTheLanguageDoesNotHaveIsRefused()
    {
        var (behaviour, document) = Document("Nope.Thing");
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "value");

        Assert.Contains("is not a concept of this language", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void ABodyNamingASlotTheConceptDoesNotHaveIsRefused()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "nope");

        Assert.Contains("is not a property", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void ABodyNamingASlotOfTheWrongKindIsRefused()
    {
        // `target` is a reference, so a property part reading it would answer nothing at every call.
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "target");

        Assert.Contains("is not a property", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void ABodyPartThatNamesNoSlotIsRefused()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Child, read: null);

        Assert.Contains("must name the slot", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void AChoiceWithoutItsOtherwisePartIsRefused()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        var choose = Part(behaviour, Function(behaviour, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Choose, read: null);
        choose.Set(BehaviourConceptKeys.SlotSlot, BehaviourTestLanguage.FlagSlot);
        choose.Set(BehaviourConceptKeys.ValueSlot, "true");
        LiteralPart(behaviour, choose, BehaviourConceptKeys.ThenSlot, "flagged");

        Assert.Contains("needs its 'otherwise' part", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    [Fact]
    public void ADescribeFunctionIsRefusedUntilItHasAConsumer()
    {
        var (behaviour, document) = Document(BehaviourTestLanguage.Node);
        var function = Function(behaviour, document, BehaviourConceptKeys.DescribeKind);
        Part(behaviour, function, BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Property, "value");

        Assert.Contains("only 'name' is lowered yet", Assert.Throws<ConceptDefinitionException>(() => Lower(document)).Message);
    }

    private static (Model Behaviour, Node Document) NewDocument()
    {
        var model = BehaviourLanguage.NewProject().NewModel("behaviour");

        return (model, model.NewNode(BehaviourConceptKeys.Document));
    }

    private static (Model Behaviour, Node Document) Document(string conceptKey)
    {
        var (model, document) = NewDocument();
        document.Set(BehaviourConceptKeys.ConceptSlot, conceptKey);

        return (model, document);
    }

    private static Node Function(Model model, Node document, string kind = BehaviourConceptKeys.NameKind)
    {
        var function = model.NewChild(document, BehaviourConceptKeys.FunctionsSlot, BehaviourConceptKeys.Function);
        function.Set(BehaviourConceptKeys.KindSlot, kind);

        return function;
    }

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

    private static Node Choice(Model model, Node document, string slot, string? value)
    {
        var choose = Part(model, Function(model, document), BehaviourConceptKeys.BodySlot, BehaviourConceptKeys.Choose, read: null);
        choose.Set(BehaviourConceptKeys.SlotSlot, slot);

        if (value is not null)
        {
            choose.Set(BehaviourConceptKeys.ValueSlot, value);
        }

        LiteralPart(model, choose, BehaviourConceptKeys.ThenSlot, "flagged");
        LiteralPart(model, choose, BehaviourConceptKeys.OtherwiseSlot, "plain");

        return choose;
    }

    private static IReadOnlyDictionary<string, Func<Node, string?>> Lower(Node document) =>
        BehaviourLowering.Lower([document], BehaviourTestLanguage.Concepts);
}
