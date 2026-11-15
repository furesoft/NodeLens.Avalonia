using Xunit;

namespace Projectional.Core.Tests;

public sealed class ConceptRegistryTests
{
    private static ConceptRegistry Create(params ConceptDeclaration[] declarations) =>
        ConceptRegistry.Create(declarations);

    [Fact]
    public void AConceptExposesItsSlotsInDeclarationOrder()
    {
        var registry = Create(new ConceptDeclaration(
            "Rules.Rule",
            [new PropertySlot("name", PropertyType.String), new PropertySlot("enabled", PropertyType.Boolean)]));

        var rule = registry.Get("Rules.Rule");

        Assert.Equal(["name", "enabled"], rule.Slots.Select(slot => slot.Name));
        Assert.Equal(PropertyType.String, rule.FindProperty("name")?.Type);
        Assert.Null(rule.FindChild("name"));
        Assert.Null(rule.FindProperty("nope"));
    }

    [Fact]
    public void InheritedSlotsComeBeforeOwnOnesAndResolveTransitively()
    {
        var registry = Create(
            new ConceptDeclaration("Base", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Mid", [new PropertySlot("order", PropertyType.Integer)], ["Base"]),
            new ConceptDeclaration("Leaf", [new PropertySlot("note", PropertyType.String)], ["Mid"]));

        var leaf = registry.Get("Leaf");

        Assert.Equal(["name", "order", "note"], leaf.Slots.Select(slot => slot.Name));
        Assert.True(leaf.IsKindOf("Leaf"));
        Assert.True(leaf.IsKindOf("Mid"));
        Assert.True(leaf.IsKindOf("Base"));
        Assert.False(leaf.IsKindOf("Rules.Rule"));
        Assert.False(registry.Get("Base").IsKindOf("Leaf"));
    }

    [Fact]
    public void ASharedSuperConceptReachedTwoWaysIsADiamondNotAConflict()
    {
        var registry = Create(
            new ConceptDeclaration("Named", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Left", superConcepts: ["Named"]),
            new ConceptDeclaration("Right", superConcepts: ["Named"]),
            new ConceptDeclaration("Both", superConcepts: ["Left", "Right"]));

        Assert.Equal(["name"], registry.Get("Both").Slots.Select(slot => slot.Name));
    }

    [Fact]
    public void TwoDifferentDeclarationsOfOneSlotNameAreRejected()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Create(
            new ConceptDeclaration("Left", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Right", [new PropertySlot("name", PropertyType.Integer)]),
            new ConceptDeclaration("Both", superConcepts: ["Left", "Right"])));

        Assert.Contains("name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RedeclaringAnInheritedSlotWithADifferentTypeIsRejected()
    {
        Assert.Throws<ConceptDefinitionException>(() => Create(
            new ConceptDeclaration("Base", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Leaf", [new PropertySlot("name", PropertyType.Integer)], ["Base"])));
    }

    [Fact]
    public void RedeclaringAnInheritedSlotIdenticallyIsHarmless()
    {
        var registry = Create(
            new ConceptDeclaration("Base", [new PropertySlot("name", PropertyType.String)]),
            new ConceptDeclaration("Leaf", [new PropertySlot("name", PropertyType.String)], ["Base"]));

        Assert.Equal(["name"], registry.Get("Leaf").Slots.Select(slot => slot.Name));
    }

    [Fact]
    public void AnUnknownSuperConceptIsRejected()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => Create(new ConceptDeclaration("Leaf", superConcepts: ["Missing"])));

        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInheritanceCycleIsRejected()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Create(
            new ConceptDeclaration("A", superConcepts: ["B"]),
            new ConceptDeclaration("B", superConcepts: ["A"])));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADuplicateConceptKeyIsRejected()
    {
        Assert.Throws<ConceptDefinitionException>(() => Create(
            new ConceptDeclaration("Rules.Rule"),
            new ConceptDeclaration("Rules.Rule")));
    }

    [Fact]
    public void ASlotWhoseTargetConceptIsNeverDeclaredIsRejected()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Create(
            new ConceptDeclaration("Rules.Rule", [new ChildSlot("outcome", "Rules.Action")])));

        Assert.Contains("Rules.Action", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SlotsMayTargetDeclaredConceptsAndAcceptSubConcepts()
    {
        var registry = Create(
            new ConceptDeclaration("Rules.Action", [new PropertySlot("text", PropertyType.String)]),
            new ConceptDeclaration("Rules.Alert", superConcepts: ["Rules.Action"]),
            new ConceptDeclaration("Rules.Rule", [new ChildSlot("outcome", "Rules.Action")]));

        var slot = registry.Get("Rules.Rule").FindChild("outcome");

        Assert.NotNull(slot);
        Assert.Equal("Rules.Action", slot.ChildConceptKey);
        Assert.True(registry.Get("Rules.Alert").IsKindOf(slot.ChildConceptKey));
    }

    [Fact]
    public void ANameSlotHasToBeAStringProperty()
    {
        var ok = Create(new ConceptDeclaration("A", [new PropertySlot("name", PropertyType.String)], nameSlot: "name"));
        Assert.Equal(PropertyType.String, ok.Get("A").NameSlot?.Type);

        Assert.Throws<ConceptDefinitionException>(
            () => Create(new ConceptDeclaration("B", [new PropertySlot("name", PropertyType.Integer)], nameSlot: "name")));

        Assert.Throws<ConceptDefinitionException>(
            () => Create(new ConceptDeclaration("C", nameSlot: "name")));

        Assert.Throws<ConceptDefinitionException>(
            () => Create(new ConceptDeclaration("D", [new ChildSlot("name", "D")], nameSlot: "name")));
    }

    [Fact]
    public void ANameSlotMayBeInherited()
    {
        var registry = Create(
            new ConceptDeclaration("Named", [new PropertySlot("name", PropertyType.String)], nameSlot: "name"),
            new ConceptDeclaration("Leaf", superConcepts: ["Named"]));

        Assert.Equal("name", registry.Get("Leaf").NameSlot?.Name);
    }

    [Fact]
    public void AConceptMayOverrideTheInheritedNameSlot()
    {
        var registry = Create(
            new ConceptDeclaration("Named", [new PropertySlot("name", PropertyType.String)], nameSlot: "name"),
            new ConceptDeclaration(
                "Leaf",
                [new PropertySlot("title", PropertyType.String)],
                ["Named"],
                nameSlot: "title"));

        Assert.Equal("title", registry.Get("Leaf").NameSlot?.Name);
        Assert.Equal("name", registry.Get("Named").NameSlot?.Name);
    }

    [Fact]
    public void ConceptsAreListedInDeclarationOrder()
    {
        var registry = Create(
            new ConceptDeclaration("Zeta"),
            new ConceptDeclaration("Alpha"),
            new ConceptDeclaration("Mid"));

        Assert.Equal(["Zeta", "Alpha", "Mid"], registry.Concepts.Select(concept => concept.Key));
    }

    [Fact]
    public void LookingUpAnUndeclaredConceptIsAnError()
    {
        var registry = Create(new ConceptDeclaration("A"));

        Assert.Throws<KeyNotFoundException>(() => registry.Get("Nope"));
        Assert.False(registry.TryGet("Nope", out _));
        Assert.False(registry.TryGet(null, out _));
        Assert.True(registry.TryGet("A", out var a));
        Assert.Equal("A", a.Key);
    }

    [Fact]
    public void SlotAndConceptNamesAreSpelledStrictly()
    {
        Assert.Throws<ArgumentException>(() => new PropertySlot("", PropertyType.String));
        Assert.Throws<ArgumentException>(() => new PropertySlot("has space", PropertyType.String));
        Assert.Throws<ArgumentException>(() => new PropertySlot("1digit", PropertyType.String));
        Assert.Throws<ArgumentException>(() => new PropertySlot("dotted.name", PropertyType.String));
        Assert.Throws<ArgumentException>(() => new ConceptDeclaration("not a key"));
        Assert.Throws<ArgumentException>(() => new ConceptDeclaration("A..B"));
        Assert.Throws<ArgumentException>(() => new ConceptDeclaration(".A"));
        Assert.Throws<ArgumentException>(() => new ConceptDeclaration("A."));
        Assert.Throws<ArgumentException>(() => new ConceptDeclaration("A", superConcepts: ["A"]));
    }

    [Fact]
    public void DuplicateSlotNamesWithinOneConceptAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ConceptDeclaration(
            "A",
            [new PropertySlot("name", PropertyType.String), new PropertySlot("name", PropertyType.String)]));
    }

    [Fact]
    public void APropertyDefaultHasToFitItsDeclaredType()
    {
        Assert.Throws<ArgumentException>(() => new PropertySlot("count", PropertyType.Integer, "many"));
        Assert.Equal("0", new PropertySlot("count", PropertyType.Integer, "0").DefaultValue);
        Assert.Null(new PropertySlot("count", PropertyType.Integer).DefaultValue);
    }

    [Fact]
    public void ReferenceSlotsDeclareWhetherTheyMayPointIntoTheirOwnSubtree()
    {
        Assert.False(new ReferenceSlot("next", "Rules.Rule").AllowOwnSubtree);
        Assert.True(new ReferenceSlot("next", "Rules.Rule", allowOwnSubtree: true).AllowOwnSubtree);
    }

    [Fact]
    public void ChildSlotsDefaultToManyAndCanBeDeclaredSingle()
    {
        Assert.Equal(Cardinality.Many, new ChildSlot("kids", "Rules.Rule").Cardinality);
        Assert.Equal(Cardinality.One, new ChildSlot("body", "Rules.Rule", Cardinality.One).Cardinality);
    }

    [Fact]
    public void AConceptDeclaresWhetherItIsAbstract()
    {
        var registry = Create(
            new ConceptDeclaration("Expr", isAbstract: true),
            new ConceptDeclaration("Lit", superConcepts: ["Expr"]));

        Assert.True(registry.Get("Expr").IsAbstract);
        Assert.False(registry.Get("Lit").IsAbstract);
    }

    [Fact]
    public void AConcreteConceptIsItsOwnDefaultAndAnUnknownOneHasNone()
    {
        var registry = Create(
            new ConceptDeclaration("Expr", isAbstract: true),
            new ConceptDeclaration("Lit", superConcepts: ["Expr"]));

        Assert.Equal("Lit", registry.DefaultInstantiableFor("Lit")?.Key);
        Assert.Null(registry.DefaultInstantiableFor("Nope"));
    }

    [Fact]
    public void AnAbstractConceptDefaultsToTheFirstConcreteConceptThatExtendsIt()
    {
        var registry = Create(
            new ConceptDeclaration("Expr", isAbstract: true),
            new ConceptDeclaration("Lit", superConcepts: ["Expr"]),
            new ConceptDeclaration("Binary", superConcepts: ["Expr"]));

        Assert.Equal("Lit", registry.DefaultInstantiableFor("Expr")?.Key);
    }

    [Fact]
    public void AnAbstractConceptWithOnlyAbstractSubConceptsHasNoDefault()
    {
        var registry = Create(
            new ConceptDeclaration("Expr", isAbstract: true),
            new ConceptDeclaration("Arithmetic", superConcepts: ["Expr"], isAbstract: true));

        Assert.Null(registry.DefaultInstantiableFor("Expr"));
        Assert.Null(registry.DefaultInstantiableFor("Arithmetic"));
    }

    [Fact]
    public void AnAbstractConceptDoesNotTakePartInSlotConformanceItself()
    {
        var registry = Create(
            new ConceptDeclaration("Expr", isAbstract: true),
            new ConceptDeclaration("Lit", superConcepts: ["Expr"]),
            new ConceptDeclaration("Rules.Rule", [new ChildSlot("condition", "Expr", Cardinality.One)]));

        var slot = registry.Get("Rules.Rule").FindChild("condition");

        Assert.NotNull(slot);
        Assert.True(registry.Get("Lit").IsKindOf(slot.ChildConceptKey));
        Assert.False(registry.Get("Expr").IsKindOf("Lit"));
    }
}
