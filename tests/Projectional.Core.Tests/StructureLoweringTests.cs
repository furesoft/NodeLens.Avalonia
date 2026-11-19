using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Structures;
using Projectional.Structures.Intentions;
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

        var registry = StructureLowering.CreateRegistry(definition.Roots);
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
            slots: [definition.Child("steps", "Rules.Step", "0..n")]);
        definition.Concept("Rules.Step", supers: ["Rules.Element"], slots: [definition.Property("text", "string")]);

        var registry = StructureLowering.CreateRegistry(definition.Roots);
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
    public void TheFourRangesLowerToTheirStorageShapeAndMinimum()
    {
        // The four spellings the notation offers are the four combinations of storage shape and lower end; the
        // upper end is the cardinality's answer, so nothing about it is written down.
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Item", slots: [definition.Property("name", "string")]);
        definition.Concept(
            "Rules.Holder",
            slots:
            [
                definition.Child("noneOrOne", "Rules.Item", "0..1"),
                definition.Child("zeroOrMore", "Rules.Item", "0..n"),
                definition.Child("exactlyOne", "Rules.Item", "1"),
                definition.Child("oneOrMore", "Rules.Item", "1..n"),
            ]);

        var holder = StructureLowering.Lower(definition.Roots).Single(concept => concept.Key == "Rules.Holder");
        var children = holder.Slots.OfType<ChildSlot>().ToArray();

        Assert.Equal(
            [Cardinality.One, Cardinality.Many, Cardinality.One, Cardinality.Many],
            children.Select(slot => slot.Cardinality));

        Assert.Equal([0, 0, 1, 1], children.Select(slot => slot.Minimum));
        Assert.Equal([1, null, 1, null], children.Select(slot => slot.Maximum));
    }

    [Fact]
    public void TheThreeSectionsAreConcatenatedInTheOrderTheSectionsAppear()
    {
        // Slot order is the concept's declaration order, because it decides the default fill and the completion
        // ranking. A definition that names a reference before a property therefore still declares the property
        // first: the section a slot was written into is its rank, not the order the editor happened to create it.
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Target", slots: [definition.Property("name", "string")]);
        definition.Concept(
            "Rules.Rule",
            slots:
            [
                definition.Reference("condition", "Rules.Target"),
                definition.Child("body", "Rules.Target"),
                definition.Property("name", "string"),
            ]);

        var declared = StructureLowering.Lower(definition.Roots).Single(c => c.Key == "Rules.Rule");

        Assert.Equal(["name", "body", "condition"], declared.Slots.Select(slot => slot.Name));
    }

    [Fact]
    public void ADeclaredEnumerationIsTheTypeOfItsProperty()
    {
        var definition = new DefinitionBuilder();

        definition.Enumeration("Rules.Outcome", "accept", "review", "reject");
        definition.Concept("Rules.Rule", slots: [definition.Property("outcome", "Rules.Outcome")]);

        var registry = StructureLowering.CreateRegistry(definition.Roots);
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

        var registry = StructureLowering.CreateRegistry(definition.Roots);

        Assert.Equal("review", registry.Get("Rules.Rule").FindProperty("outcome")!.DefaultValue);
    }

    [Fact]
    public void ADescribedSlotTravelsWithItsSentenceAndTheOptInOverAValue()
    {
        // Both halves of the documentation are data: the sentence, and whether a hover shows it over a value too.
        // A declared language that could write only one of them would be weaker than a hand-written one.
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Rules.Rule",
            slots:
            [
                definition.Property(
                    "name",
                    "string",
                    description: "The name this rule is known by.",
                    showDescriptionWhenFilled: true),
            ]);

        var registry = StructureLowering.CreateRegistry(definition.Roots);
        var name = registry.Get("Rules.Rule").FindProperty("name")!;

        Assert.Equal("The name this rule is known by.", name.Description);
        Assert.True(name.ShowDescriptionWhenFilled);
    }

    [Fact]
    public void AReferenceSlotPointsAtAnotherConcept()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Reference("condition", "Rules.Condition")]);
        definition.Concept("Rules.Condition", slots: [definition.Property("text", "string")]);

        var registry = StructureLowering.CreateRegistry(definition.Roots);

        Assert.Equal("Rules.Condition", registry.Get("Rules.Rule").FindReference("condition")!.TargetConceptKey);
    }

    [Fact]
    public void ADefinitionIsDrawnLikeAnyOtherModel()
    {
        // The rule of the plan: if the definition language needed a rendering exception it would not be a real
        // language. Each document holds one definition, so each is drawn as that definition - the header says what
        // it is, the options say what it allows, and the three sections say what it holds.
        var definition = new DefinitionBuilder();

        var outcome = definition.Enumeration("Rules.Outcome", "accept");
        var rule = definition.Concept("Rules.Rule", slots: [definition.Property("name", "string")]);

        var notations = StructureNotations.Create();

        Assert.Equal(
            string.Join('\n', "enum Rules.Outcome", "  accept"),
            LayoutSnapshot.Render(outcome, notations, width: 120));

        Assert.Equal(
            string.Join(
                '\n',
                "concept Rules.Rule {",
                "  offered in completion: true",
                "  named by:",
                "  collapsed by:",
                "  described as:",
                "  properties:",
                "    <<property name : string",
                "      default:",
                "      described as:",
                "      described when filled: false>>",
                "  children:",
                "    <<child>>",
                "  references:",
                "    <<reference>>",
                "}"),
            LayoutSnapshot.Render(rule, notations, width: 120));
    }

    [Fact]
    public void AnAbstractConceptSaysSoInItsHeader()
    {
        // MPS' spelling: `abstract` is a word of the header, not an option of the concept, so the same notation
        // draws both concepts and the conditional cell is what makes the difference.
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Element", isAbstract: true);

        var drawn = LayoutSnapshot.Render(definition.Root, StructureNotations.Create(), width: 120);

        Assert.StartsWith("abstract concept Rules.Element", drawn, StringComparison.Ordinal);

        // And the flag itself is not drawn as a value anywhere, which is the point of moving it into the header.
        Assert.DoesNotContain("is abstract", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void AConcreteConceptDoesNotSayAbstract()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule");

        var drawn = LayoutSnapshot.Render(definition.Root, StructureNotations.Create(), width: 120);

        Assert.StartsWith("concept Rules.Rule", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("abstract", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void AChildSlotIsDrawnWithItsRangeInBrackets()
    {
        // The one cell MPS offers for a cardinality: the range an author reads, right after the concept it holds.
        // The declaration also says its own kind and closes where it ends, so a section with several slots needs
        // no heading to be read correctly.
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Step", slots: [definition.Property("text", "string")]);

        var rule = definition.Concept("Rules.Rule", slots: [definition.Child("steps", "Rules.Step", "1..n")]);

        var drawn = LayoutSnapshot.Render(rule, StructureNotations.Create(), width: 120);

        Assert.Contains("<<child steps : Rules.Step[1..n]>>", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeaderCarriesTheInheritanceAndTheBodyIsABlock()
    {
        // MPS' reading of a concept: what it extends and implements in the header, what it allows and holds inside
        // braces. A list that holds nothing is not drawn at all, so the header says only what is true of this
        // concept - which is why the language ships the two intentions that create a first entry.
        var definition = new DefinitionBuilder();

        var empty = definition.Concept("Rules.Element");

        var comparison = definition.Concept(
            "Rules.Comparison",
            supers: ["Rules.Element", "Framework.Base"],
            implements: ["Framework.Named"]);

        var notations = StructureNotations.Create();

        Assert.Equal(
            string.Join(
                '\n',
                "concept Rules.Element {",
                "  offered in completion: true",
                "  named by:",
                "  collapsed by:",
                "  described as:",
                "  properties:",
                "    <<property>>",
                "  children:",
                "    <<child>>",
                "  references:",
                "    <<reference>>",
                "}"),
            LayoutSnapshot.Render(empty, notations, width: 120));

        Assert.StartsWith(
            "concept Rules.Comparison extends Rules.Element, Framework.Base, implements Framework.Named {",
            LayoutSnapshot.Render(comparison, notations, width: 120),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDeclarationOfASectionStartsANewLine()
    {
        // A collection lays out inline unless the notation asks it to stack, so before P4 two properties of one
        // concept were drawn side by side and an enumeration's symbols shared a line. A definition is read one
        // declaration per line, which is the shape a reviewer expects of a structure model.
        var definition = new DefinitionBuilder();

        var rule = definition.Concept(
            "Rules.Rule",
            slots:
            [
                definition.Property("name", "string"),
                definition.Property("outcome", "Rules.Outcome"),
                definition.Child("steps", "Rules.Step"),
                definition.Child("body", "Rules.Body"),
                definition.Reference("condition", "Rules.Condition"),
                definition.Reference("other", "Rules.Condition"),
            ]);

        var lines = LayoutSnapshot.Render(rule, StructureNotations.Create(), width: 160).Split('\n');

        Assert.Contains("    <<property name : string", lines);
        Assert.Contains("    <<property outcome : Rules.Outcome", lines);
        Assert.Contains("    <<child steps : Rules.Step[1]>>", lines);
        Assert.Contains("    <<child body : Rules.Body[1]>>", lines);
        Assert.Contains("    <<reference condition : Rules.Condition", lines);
        Assert.Contains("    <<reference other : Rules.Condition", lines);
        Assert.Contains("      own subtree: false>>", lines);
    }

    [Fact]
    public void AnEmptyInheritanceIsNotDrawnAndLeavesNoCell()
    {
        // The price of the header shape: a list that is empty is not drawn, so nothing addresses the slot - not an
        // empty cell, not a placeholder. That is why the language owes a second way in, and it is what these two
        // tests pin.
        var definition = new DefinitionBuilder();
        var concept = definition.Concept("Rules.Rule");

        var notations = StructureNotations.Create();

        var drawn = LayoutSnapshot.Render(concept, notations, width: 120);

        Assert.StartsWith("concept Rules.Rule {", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("extends", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("implements", drawn, StringComparison.Ordinal);

        Assert.DoesNotContain(
            CaretPlaces.Of(new EditorTreeBuilder(notations).Build(concept)),
            place => place.Address.Equals(CellAddress.OfSlot(concept.Id, StructureLanguage.SuperTypeSlot)));
    }

    [Fact]
    public void TheIntentionIsTheWayInToTheFirstSuperConcept()
    {
        // "Inherit from…" is what makes the hidden list reachable: it creates the entry and leaves the caret on the
        // new key, so the name is typed straight away instead of being searched for.
        var definition = new DefinitionBuilder();
        var concept = definition.Concept("Rules.Rule");

        using var session = new EditorSession(concept, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(new CellAddress(concept.Id)));

        var offer = IntentionList.For(session).Single(entry => entry.Id == StructureIntentions.InheritFromId);

        Assert.True(offer.Availability.IsApplicable);
        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);

        var superType = Assert.Single(concept.GetChildren(StructureLanguage.SuperTypeSlot));

        Assert.Equal(StructureConceptKeys.SuperType, superType.Concept.Key);
        Assert.Equal(CellAddress.OfSlot(superType.Id, "conceptKey"), session.Selection.Anchor.Address);

        // From now on the list is on screen, which is what makes the entry absent rather than a second way to do
        // what the caret in the list already does.
        Assert.Contains("extends", LayoutSnapshot.Render(session.Layout, width: 120), StringComparison.Ordinal);
    }

    [Fact]
    public void TheInheritanceEntryAddsASecondEntryToAFilledList()
    {
        // The entry stays available once the list is on screen, and that is not decoration: Enter does not extend
        // an inline collection - InsertNodeAction fills a slot that is empty or adds a sibling to a block child, and
        // a super concept's key is neither. Without the entry a second super concept could not be written at all.
        var definition = new DefinitionBuilder();

        var concept = definition.Concept("Rules.Rule", supers: ["Rules.Element"]);

        using var session = new EditorSession(concept, StructureNotations.Create());

        session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
        session.Select(Selection.At(new CellAddress(concept.Id)));

        var offer = IntentionList.For(session).Single(entry => entry.Id == StructureIntentions.InheritFromId);

        Assert.True(offer.Availability.IsApplicable);
        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);

        Assert.Equal(2, concept.GetChildren(StructureLanguage.SuperTypeSlot).Count);

        // The first one is untouched, the new one is second, and the caret is on its key.
        var added = concept.GetChildren(StructureLanguage.SuperTypeSlot)[1];

        Assert.Equal(CellAddress.OfSlot(added.Id, "conceptKey"), session.Selection.Anchor.Address);
    }

    [Fact]
    public void AnEnumerationsSymbolsStandOnePerLine()
    {
        // The symbols are a `Many` slot like any other: the order is the order they are offered in, and reading
        // them off one line would hide a symbol added in the middle.
        var definition = new DefinitionBuilder();

        var outcome = definition.Enumeration("Rules.Outcome", "accept", "review");

        Assert.Equal(
            string.Join('\n', "enum Rules.Outcome", "  accept", "  review"),
            LayoutSnapshot.Render(outcome, StructureNotations.Create(), width: 120));
    }

    [Fact]
    public void TheCardinalityCellOffersTheFourRanges()
    {
        // What the cell offers on arrival is what the enumeration declares, in declaration order - the list an
        // author picks from, and the only four things a slot can say.
        var cardinality = StructureLanguage.Concepts.Get(StructureConceptKeys.ChildDecl).FindProperty("cardinality");

        Assert.NotNull(cardinality);
        Assert.Equal(["0..1", "0..n", "1", "1..n"], cardinality.Type.Symbols);
    }

    [Fact]
    public void APropertyThatNamesNoTypeIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Property("name", "")]);

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower(definition.Roots));

        Assert.Contains("does not say its 'typeName'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyThatNamesAnUnknownTypeIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", slots: [definition.Property("name", "text")]);

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower(definition.Roots));

        Assert.Contains("'text'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declared enumeration", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEnumerationWithoutSymbolsIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Enumeration("Rules.Outcome");

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower(definition.Roots));

        Assert.Contains("declares no symbols", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoConceptsWithOneKeyAreRefused()
    {
        // `ConceptDeclaration` owns the key's shape, so a duplicate is caught by the registry that builds them.
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule");
        definition.Concept("Rules.Rule");

        Assert.Throws<ConceptDefinitionException>(() => StructureLowering.CreateRegistry(definition.Roots));
    }

    [Fact]
    public void ADefinedSuperConceptThatDoesNotExistIsRefused()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Rule", supers: ["Rules.Nope"]);

        Assert.Throws<ConceptDefinitionException>(() => StructureLowering.CreateRegistry(definition.Roots));
    }

    [Fact]
    public void ADocumentThatIsNotADefinitionIsRefused()
    {
        var model = StructureLanguage.NewModel();
        var slot = model.NewNode(StructureConceptKeys.PropertyDecl);

        model.AddRoot(slot);

        var error = Assert.Throws<ConceptDefinitionException>(() => StructureLowering.Lower([slot]));

        Assert.Contains("is not a definition", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnumerationsFromOtherDocumentsAreVisibleToEveryConcept()
    {
        // A property may name an enumeration that lives in another file, which is why the lowering reads every
        // enumeration before it lowers the first concept.
        var definition = new DefinitionBuilder();

        definition.Concept(
            "Rules.Rule",
            slots: [definition.Property("name", "string"), definition.Property("status", "Rules.Status")],
            nameSlot: "name");

        definition.Enumeration("Rules.Status", "draft", "approved");

        var registry = StructureLowering.CreateRegistry(definition.Roots);

        Assert.True(registry.TryGet("Rules.Rule", out var rule));
        Assert.Equal(["draft", "approved"], Assert.IsType<PropertySlot>(rule!.Slots[1]).Type.Symbols);
    }

    [Fact]
    public void AConceptThatImplementsAnInterfaceLowersToIt()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Target", isAbstract: true, isInterface: true);

        definition.Concept(
            "Rules.Rule",
            slots: [definition.Property("name", "string")],
            nameSlot: "name",
            implements: ["Rules.Target"]);

        var registry = StructureLowering.CreateRegistry(definition.Roots);
        var rule = registry.Get("Rules.Rule");

        // Two walks, one relation: the interface is conformance, and it is not an extension.
        Assert.Equal(["Rules.Target"], rule.Interfaces.Select(implemented => implemented.Key));
        Assert.Empty(rule.Extensions);
        Assert.Equal(["Rules.Target"], rule.SuperConcepts.Select(super => super.Key));
        Assert.True(rule.IsKindOf("Rules.Target"));
        Assert.True(registry.Get("Rules.Target").IsInterface);
    }

    [Fact]
    public void AnInterfaceThatIsNotAbstractIsRefusedWhereItWasWritten()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Target", isAbstract: false, isInterface: true);

        var error = Assert.Throws<ConceptDefinitionException>(
            () => StructureLowering.CreateRegistry(definition.Roots));

        Assert.Contains("abstract by definition", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInterfaceSaysInterfaceInsteadOfAbstract()
    {
        var definition = new DefinitionBuilder();

        definition.Concept("Rules.Target", isAbstract: true, isInterface: true);
        definition.Concept("Rules.Base", isAbstract: true);
        definition.Concept("Rules.Rule");

        var notations = StructureNotations.Create();
        var target = definition.Roots[0];

        Assert.StartsWith(
            "interface concept Rules.Target",
            LayoutSnapshot.Render(target, notations, width: 120),
            StringComparison.Ordinal);

        // An interface is abstract by definition, so the other word would say the same thing twice.
        Assert.DoesNotContain("abstract", LayoutSnapshot.Render(target, notations, width: 120), StringComparison.Ordinal);
        Assert.StartsWith(
            "abstract concept Rules.Base",
            LayoutSnapshot.Render(definition.Roots[1], notations, width: 120),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "concept Rules.Rule",
            LayoutSnapshot.Render(definition.Roots[2], notations, width: 120),
            StringComparison.Ordinal);
    }
}
