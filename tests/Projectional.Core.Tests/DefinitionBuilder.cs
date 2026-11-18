using Projectional.Core;
using Projectional.Structures;

namespace Projectional.Core.Tests;

/// <summary>
/// Writes a language definition the way a user would: one document per definition, nodes created through it.
/// </summary>
/// <remarks>
/// <para>
/// A helper rather than five static methods because every slot of a concept has to belong to the same model as the
/// concept - the model refuses a child from another one, and a fixture that built each node separately would fail on
/// the first insert for a reason that has nothing to do with the test.
/// </para>
/// <para>
/// Each definition is a document of its own, which is what a language project stores: one concept or one enumeration
/// per file. The builder therefore keeps a document per definition and the slots always go into the current one,
/// which is the document that was created last.
/// </para>
/// </remarks>
internal sealed class DefinitionBuilder
{
    private readonly Project _project = StructureLanguage.NewProject();
    private readonly List<Model> _documents = [];
    private readonly List<Node> _roots = [];

    private Model _current;

    public DefinitionBuilder() => _current = NewDocument();

    /// <summary>The first definition's root, which is what a single-definition test lowers.</summary>
    public Node Root => _roots[0];

    /// <summary>The root of every definition, in the order they were written.</summary>
    public IReadOnlyList<Node> Roots => _roots;

    /// <summary>One document per definition, in the order they were written.</summary>
    public IReadOnlyList<Model> Documents => _documents;

    /// <summary>Adds a concept to the definition and answers the node, so slots can be added easily.</summary>
    public Node Concept(
        string key,
        IReadOnlyList<SlotSpec>? slots = null,
        IReadOnlyList<string>? supers = null,
        bool isAbstract = false,
        string? nameSlot = null,
        string? description = null,
        string? collapseSlot = null)
    {
        var concept = Start(StructureConceptKeys.Concept);

        concept.Set(StructureLanguage.KeySlot, key);
        concept.Set(StructureLanguage.IsAbstractProperty, isAbstract ? "true" : "false");

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
            concept.Model.NewChild(concept, StructureLanguage.SuperTypeSlot, StructureConceptKeys.SuperType)
                .Set("conceptKey", super);
        }

        foreach (var slot in slots ?? [])
        {
            Add(concept, slot);
        }

        return concept;
    }

    /// <summary>A property slot, described so that it can be written into the concept's own document.</summary>
    public SlotSpec Property(
        string name,
        string typeName,
        string? defaultValue = null,
        string? description = null,
        bool showDescriptionWhenFilled = false) =>
        Slot(new SlotSpec(
            StructureConceptKeys.PropertyDecl,
            name,
            typeName,
            defaultValue,
            description,
            ShowDescriptionWhenFilled: showDescriptionWhenFilled));

    /// <summary>A reference slot.</summary>
    public SlotSpec Reference(string name, string targetConceptKey, bool allowOwnSubtree = false) =>
        Slot(new SlotSpec(
            StructureConceptKeys.ReferenceDecl,
            name,
            TargetConceptKey: targetConceptKey,
            AllowOwnSubtree: allowOwnSubtree));

    /// <summary>A child slot.</summary>
    public SlotSpec Child(string name, string childConceptKey, string cardinality = "1") =>
        Slot(new SlotSpec(
            StructureConceptKeys.ChildDecl,
            name,
            ChildConceptKey: childConceptKey,
            Cardinality: cardinality));

    /// <summary>Checks that a slot's kind is a concept the structure language declares.</summary>
    /// <remarks>
    /// A fixture that named a kind the language does not have would write a document nothing can load, and the
    /// failure would surface in whichever test happened to load it. Asking the registry here is the same guarantee
    /// the notation registry gives a language author: the earliest moment the answer exists.
    /// </remarks>
    private SlotSpec Slot(SlotSpec slot)
    {
        _project.Concepts.Get(slot.ConceptKey);

        return slot;
    }

    /// <summary>Writes one described slot into <paramref name="concept"/>'s own document.</summary>
    /// <remarks>
    /// The three kinds go into their own collections, which is what a language author's <c>properties:</c> /
    /// <c>children:</c> / <c>references:</c> sections are.
    /// </remarks>
    private static void Add(Node concept, SlotSpec slot)
    {
        var model = concept.Model;

        var node = model.NewChild(
            concept,
            slot.ConceptKey switch
            {
                StructureConceptKeys.PropertyDecl => StructureLanguage.PropertiesSlot,
                StructureConceptKeys.ChildDecl => StructureLanguage.ChildrenSlot,
                _ => StructureLanguage.ReferencesSlot,
            },
            slot.ConceptKey);

        node.Set("name", slot.Name);

        switch (slot.ConceptKey)
        {
            case StructureConceptKeys.PropertyDecl:
                node.Set("typeName", slot.TypeName!);

                if (slot.DefaultValue is not null)
                {
                    node.Set("defaultValue", slot.DefaultValue);
                }

                if (slot.Description is not null)
                {
                    node.Set("description", slot.Description);
                }

                if (slot.ShowDescriptionWhenFilled)
                {
                    node.Set("showDescriptionWhenFilled", "true");
                }

                break;

            case StructureConceptKeys.ChildDecl:
                node.Set("childConceptKey", slot.ChildConceptKey!);
                node.Set("cardinality", slot.Cardinality ?? "1");
                break;

            default:
                node.Set("targetConceptKey", slot.TargetConceptKey!);

                if (slot.AllowOwnSubtree)
                {
                    node.Set("allowOwnSubtree", "true");
                }

                break;
        }
    }

    /// <summary>An enumeration with its symbols, as a document of its own.</summary>
    public Node Enumeration(string key, params string[] symbols)
    {
        var enumeration = Start(StructureConceptKeys.Enumeration);

        enumeration.Set(StructureLanguage.KeySlot, key);

        foreach (var symbol in symbols)
        {
            enumeration.Model.NewChild(enumeration, "symbols", StructureConceptKeys.EnumSymbol).Set("symbol", symbol);
        }

        return enumeration;
    }

    /// <summary>Starts a document whose root is one empty definition, and makes it the current one.</summary>
    private Node Start(string conceptKey)
    {
        var root = Current().NewNode(conceptKey);

        _roots.Add(root);

        return root;
    }

    /// <summary>The document the next definition is written into.</summary>
    /// <remarks>
    /// A document holds exactly one definition, so the builder advances as soon as the current document has a root.
    /// <see cref="Model.NewNode"/> already makes the new node a root, which is why nothing here re-parents it.
    /// </remarks>
    private Model Current()
    {
        if (_current.Roots.Count > 0)
        {
            _current = NewDocument();
        }

        return _current;
    }

    /// <summary>A new empty document, which the next definition is written into.</summary>
    private Model NewDocument()
    {
        var model = _project.NewModel($"definition{_documents.Count + 1}");

        _documents.Add(model);

        return model;
    }
}
