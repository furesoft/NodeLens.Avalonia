using Projectional.Core;
using Projectional.Projection;
using Projectional.Structures;
using Projectional.Structures.Notation;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language written as a model, lowered into the declarations every other layer already understands.
/// </summary>
/// <remarks>
/// The point of the slice is that nothing downstream learns where a concept came from, so the tests are written
/// from the outside: build a definition, lower it, and then use the result exactly as a hand-written language
/// would be used - create nodes with it, watch it refuse what the definition forbids, and render it.
/// </remarks>
public sealed class StructureLoweringTests
{
    [Fact]
    public void ADeclaredConceptBecomesAConceptThatCanHoldNodes()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Property("name", "string")], nameSlot: "name");

        var registry = StructureLowering.CreateRegistry(definition.Root);
        var model = new Project(registry).NewModel();
        var rule = model.NewNode("Rules.Rule");

        rule.Set("name", "Approved");

        Assert.Equal("Rules.Rule", rule.Concept.Key);
        Assert.Equal("Approved", rule.GetOrNull("name"));

        // A name slot is what a reference to the node shows, so the definition says it too.
        Assert.Equal("Approved", rule.Name);
    }

    [Fact]
    public void InheritanceAndCardinalityAreWhatTheDefinitionSaid()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Element", isAbstract: true, slots: [definition.Property("name", "string")]);
        definition.Concept(
            "Rules.Rule",
            supers: ["Rules.Element"],
            slots: [definition.Child("steps", "Rules.Step", "many")]);
        definition.Concept("Rules.Step", supers: ["Rules.Element"], slots: [definition.Property("text", "string")]);

        var registry = StructureLowering.CreateRegistry(definition.Root);
        var model = new Project(registry).NewModel();

        Assert.True(registry.Get("Rules.Element").IsAbstract);
        Assert.True(registry.Get("Rules.Rule").IsKindOf("Rules.Element"));

        var rule = model.NewNode("Rules.Rule");
        var first = model.NewChild(rule, "steps", "Rules.Step");
        var second = model.NewChild(rule, "steps", "Rules.Step");

        Assert.Equal([first, second], rule.GetChildren("steps"));

        // A slot declared over a concrete concept refuses something else, exactly as a hand-written one would.
        var holder = model.NewNode("Rules.Rule");

        Assert.Throws<ModelInvariantException>(() => model.NewChild(holder, "steps", "Rules.Rule"));
    }

    [Fact]
    public void ADeclaredEnumerationIsTheTypeOfItsProperty()
    {
        var definition = new DefinitionBuilder();

        definition.Enumeration("Rules.Outcome", "accept", "review", "reject");
        definition.Concept("Rules.Rule", slots: [definition.Property("outcome", "Rules.Outcome")]);

        var registry = StructureLowering.CreateRegistry(definition.Root);
        var model = new Project(registry).NewModel();
        var rule = model.NewNode("Rules.Rule");

        rule.Set("outcome", "review");

        Assert.Equal("review", rule.GetOrNull("outcome"));
        Assert.Throws<ModelInvariantException>(() => rule.Set("outcome", "signed-off"));
    }

    [Fact]
    public void ADefaultValueTravelsWithTheDeclaration()
    {
        var definition = new DefinitionBuilder();

        definition.Enumeration("Rules.Outcome", "accept", "review");
        definition.Concept(
            "Rules.Rule",
            slots: [definition.Property("outcome", "Rules.Outcome", defaultValue: "review")]);

        var registry = StructureLowering.CreateRegistry(definition.Root);

        Assert.Equal("review", registry.Get("Rules.Rule").FindProperty("outcome")!.DefaultValue);
    }

    [Fact]
    public void AReferenceSlotPointsAtAnotherConcept()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Reference("condition", "Rules.Condition")]);
        definition.Concept("Rules.Condition", slots: [definition.Property("text", "string")]);

        var registry = StructureLowering.CreateRegistry(definition.Root);

        Assert.Equal("Rules.Condition", registry.Get("Rules.Rule").FindReference("condition")!.TargetConceptKey);
    }

    [Fact]
    public void ADefinitionIsDrawnLikeAnyOtherModel()
    {
        // The rule of the plan: if the definition language needed a rendering exception it would not be a real
        // language. This is the snapshot of the provisional shape.
        var definition = new DefinitionBuilder();

        definition.Enumeration("Rules.Outcome", "accept");
        definition.Concept("Rules.Rule", slots: [definition.Property("name", "string")]);

        Assert.Equal(
            string.Join(
                '\n',
                "enum Rules.Outcome",
                "  accept",
                "concept Rules.Rule abstract=false offered=true",
                "  named by",
                "  described as",
                "  <no super concepts>",
                "  property name : string",
                "    default",
                "    described as"),
            LayoutSnapshot.Render(definition.Root, StructureNotations.Create(), width: 120));
    }

    [Fact]
    public void APropertyThatNamesNoTypeIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Property("name", "")]);

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower(definition.Root));

        Assert.Contains("does not say its 'typeName'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyThatNamesAnUnknownTypeIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Property("name", "text")]);

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower(definition.Root));

        Assert.Contains("'text'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declared enumeration", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEnumerationWithoutSymbolsIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Enumeration("Rules.Outcome");

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower(definition.Root));

        Assert.Contains("declares no symbols", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoConceptsWithOneKeyAreRefused()
    {
        // `ConceptDeclaration` owns the key's shape, so a duplicate is caught by the registry that builds them.
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule");
        definition.Concept("Rules.Rule");

        Assert.Throws<ConceptDefinitionException>(() => StructureLowering.CreateRegistry(definition.Root));
    }

    [Fact]
    public void ADefinedSuperConceptThatDoesNotExistIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", supers: ["Rules.Nope"]);

        Assert.Throws<ConceptDefinitionException>(() => StructureLowering.CreateRegistry(definition.Root));
    }

    [Fact]
    public void ADocumentThatIsNotADefinitionIsRefused()
    {
        var model = StructureLanguage.NewModel();
        var concept = model.NewNode(StructureConceptKeys.Concept);

        model.AddRoot(concept);

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower(concept));

        Assert.Contains("is not a language definition", error.Message, StringComparison.Ordinal);
    }
}
