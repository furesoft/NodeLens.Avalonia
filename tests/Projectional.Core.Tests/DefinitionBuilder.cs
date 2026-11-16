using Projectional.Core;
using Projectional.Structures;

namespace Projectional.Core.Tests;

/// <summary>
/// Writes a language definition the way a user would: one model, nodes created through it.
/// </summary>
/// <remarks>
/// A helper rather than five static methods because every node of a definition has to belong to the same model -
/// the model refuses a child from another one, and a fixture that built each node separately would fail on the
/// first insert for a reason that has nothing to do with the test.
/// </remarks>
internal sealed class DefinitionBuilder
{
    private readonly Model _model = StructureLanguage.NewModel();

    /// <summary>The root the definition is written into, created on first use.</summary>
    public Node Root { get; }

    public DefinitionBuilder() => Root = _model.NewNode(StructureConceptKeys.Language);

    public DefinitionBuilder(Node root) => Root = root;

    /// <summary>Adds a concept to the definition and answers the node, so slots can be added easily.</summary>
    public Node Concept(
        string key,
        IReadOnlyList<Node>? slots = null,
        IReadOnlyList<string>? supers = null,
        bool isAbstract = false,
        string? nameSlot = null,
        string? description = null,
        string? collapseSlot = null)
    {
        var concept = _model.NewChild(Root, StructureLanguage.ConceptSlot, StructureConceptKeys.Concept);

        concept.Set(StructureLanguage.KeySlot, key);
        concept.Set("isAbstract", isAbstract ? "true" : "false");

        if (nameSlot is not null)
        {
            concept.Set("nameSlot", nameSlot);
        }

        if (description is not null)
        {
            concept.Set("description", description);
        }

        if (collapseSlot is not null)
        {
            concept.Set("collapseSlot", collapseSlot);
        }

        foreach (var super in supers ?? [])
        {
            _model.NewChild(concept, StructureLanguage.SuperTypeSlot, StructureConceptKeys.SuperType)
                .Set("conceptKey", super);
        }

        foreach (var slot in slots ?? [])
        {
            // Each kind goes into its own collection, which is what a language author's `properties:` /
            // `children:` / `references:` sections are.
            concept.AddChild(
                slot.Concept.Key switch
                {
                    StructureConceptKeys.PropertyDecl => StructureLanguage.PropertiesSlot,
                    StructureConceptKeys.ChildDecl => StructureLanguage.ChildrenSlot,
                    _ => StructureLanguage.ReferencesSlot,
                },
                slot);
        }

        return concept;
    }

    /// <summary>A property slot, which belongs to the same model as the concept that will hold it.</summary>
    public Node Property(string name, string typeName, string? defaultValue = null, string? description = null)
    {
        var property = _model.NewNode(StructureConceptKeys.PropertyDecl);

        property.Set("name", name);
        property.Set("typeName", typeName);

        if (defaultValue is not null)
        {
            property.Set("defaultValue", defaultValue);
        }

        if (description is not null)
        {
            property.Set("description", description);
        }

        return property;
    }

    /// <summary>A reference slot.</summary>
    public Node Reference(string name, string targetConceptKey, bool allowOwnSubtree = false)
    {
        var reference = _model.NewNode(StructureConceptKeys.ReferenceDecl);

        reference.Set("name", name);
        reference.Set("targetConceptKey", targetConceptKey);

        if (allowOwnSubtree)
        {
            reference.Set("allowOwnSubtree", "true");
        }

        return reference;
    }

    /// <summary>A child slot.</summary>
    public Node Child(string name, string childConceptKey, string cardinality = "one")
    {
        var child = _model.NewNode(StructureConceptKeys.ChildDecl);

        child.Set("name", name);
        child.Set("childConceptKey", childConceptKey);
        child.Set("cardinality", cardinality);

        return child;
    }

    /// <summary>An enumeration with its symbols, added to the definition.</summary>
    public Node Enumeration(string key, params string[] symbols)
    {
        var enumeration = _model.NewChild(
            Root,
            StructureLanguage.EnumerationSlot,
            StructureConceptKeys.Enumeration);

        enumeration.Set(StructureLanguage.KeySlot, key);

        foreach (var symbol in symbols)
        {
            _model.NewChild(enumeration, "symbols", StructureConceptKeys.EnumSymbol).Set("symbol", symbol);
        }

        return enumeration;
    }
}
