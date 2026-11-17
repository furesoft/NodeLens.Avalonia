using Projectional.Core;

namespace Projectional.Structures;

/// <summary>
/// Turns a written language definition into the <see cref="ConceptDeclaration"/>s every other layer already
/// understands.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of the declared path in one type: a definition is a model, and lowering is the walk from
/// nodes to the same declarations a C# language writes by hand. Nothing below this point knows where a concept
/// came from - no second registry, no declared-only node kind - which is what
/// [ADR 0014](../../docs/adr/0014-both-authoring-paths-are-permanent.md) requires.
/// </para>
/// <para>
/// It checks what the C# compiler would have checked for a hand-written language: that every property names a
/// type, that every enumeration has symbols, that no key is declared twice. What it deliberately does
/// <em>not</em> check is the graph - whether a super concept exists, whether a child slot names a declared
/// concept - because <see cref="ConceptRegistry.Create"/> is the single validator for that and a second one here
/// would eventually disagree with it.
/// </para>
/// </remarks>
public static class StructureLowering
{
    /// <summary>The declarations a language's definition documents describe, in the order they were written.</summary>
    /// <param name="definitions">
    /// The roots of the language's structure documents: one <see cref="StructureConceptKeys.Concept"/> or one
    /// <see cref="StructureConceptKeys.Enumeration"/> per document, in the order the files are read.
    /// </param>
    /// <returns>One declaration per concept root, in that order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definitions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">
    /// A root is neither a concept nor an enumeration, a concept or enumeration key is declared twice, a property
    /// names no type or an unknown one, an enumeration holds no symbols, or a child or reference slot names no
    /// concept.
    /// </exception>
    /// <remarks>
    /// The enumerations are collected before any concept is lowered, because a property may be declared over an
    /// enumeration that lives in another document - the reason the order is two passes rather than one walk.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> Lower(IEnumerable<Node> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var roots = definitions as IReadOnlyList<Node> ?? [.. definitions];
        var enums = Enumerations(roots);
        var declarations = new List<ConceptDeclaration>();

        foreach (var root in roots)
        {
            switch (root.Concept.Key)
            {
                case StructureConceptKeys.Concept:
                    declarations.Add(LowerConcept(root, enums));
                    break;

                case StructureConceptKeys.Enumeration:
                    break;

                default:
                    throw new ConceptDefinitionException(
                        $"'{root.Concept.Key}' is not a definition; a structure document holds one concept or one enumeration.");
            }
        }

        return declarations;
    }

    /// <summary>The registry those declarations build, which validates the graph between them.</summary>
    /// <param name="definitions">The roots of the structure documents.</param>
    /// <exception cref="ConceptDefinitionException">
    /// A definition is malformed, or the graph it describes is not a valid language.
    /// </exception>
    public static ConceptRegistry CreateRegistry(IEnumerable<Node> definitions) =>
        ConceptRegistry.Create(Lower(definitions));

    /// <summary>The declared enumerations, keyed by name, so a property can be declared over one.</summary>
    private static Dictionary<string, IReadOnlyList<string>> Enumerations(IReadOnlyList<Node> roots)
    {
        var enums = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var declaration in roots.Where(root => root.Concept.Key == StructureConceptKeys.Enumeration))
        {
            var key = Required(declaration, StructureLanguage.KeySlot, "enumeration");

            var symbols = declaration
                .GetChildren("symbols")
                .Select(symbol => Required(symbol, "symbol", $"'{key}'"))
                .ToList();

            if (symbols.Count == 0)
            {
                throw new ConceptDefinitionException(
                    $"Enumeration '{key}' declares no symbols, so no property can be declared over it.");
            }

            if (!enums.TryAdd(key, symbols))
            {
                throw new ConceptDefinitionException($"Enumeration '{key}' is declared twice.");
            }
        }

        return enums;
    }

    private static ConceptDeclaration LowerConcept(Node concept, Dictionary<string, IReadOnlyList<string>> enums)
    {
        var key = Required(concept, StructureLanguage.KeySlot, "concept");

        // The key's own rules are `ConceptDeclaration`'s business; a malformed one is turned into the same
        // `ConceptDefinitionException` as everything else here, so a definition that is wrong reads as one error.
        return new ConceptDeclaration(
            key,
            // Properties, then children, then references: the order MPS writes them in and the order a completion
            // entry or a default fill sees, so the sections and the declaration order agree.
            [
                .. concept.GetChildren(StructureLanguage.PropertiesSlot).Select(slot => LowerSlot(key, slot, enums)),
                .. concept.GetChildren(StructureLanguage.ChildrenSlot).Select(slot => LowerSlot(key, slot, enums)),
                .. concept.GetChildren(StructureLanguage.ReferencesSlot).Select(slot => LowerSlot(key, slot, enums)),
            ],
            concept.GetChildren(StructureLanguage.SuperTypeSlot)
                .Select(super => Required(super, "conceptKey", $"'{key}'")),
            nameSlot: Optional(concept, "nameSlot"),
            isAbstract: Flag(concept, StructureLanguage.IsAbstractProperty),
            isOfferedInCompletion: Flag(concept, "isOfferedInCompletion"),
            description: Optional(concept, "description"),
            collapseSlot: Optional(concept, "collapseSlot"));
    }

    private static SlotDeclaration LowerSlot(string owner, Node slot, Dictionary<string, IReadOnlyList<string>> enums)
    {
        var name = Required(slot, "name", $"concept '{owner}'");

        return slot.Concept.Key switch
        {
            StructureConceptKeys.PropertyDecl => new PropertySlot(
                name,
                TypeNamed(Required(slot, "typeName", $"slot '{name}'"), owner, name, enums),
                Optional(slot, "defaultValue"),
                Optional(slot, "description"),
                Flag(slot, "showDescriptionWhenFilled")),

            StructureConceptKeys.ChildDecl => new ChildSlot(
                name,
                Required(slot, "childConceptKey", $"slot '{name}'"),
                Optional(slot, "cardinality") == "many" ? Cardinality.Many : Cardinality.One),

            StructureConceptKeys.ReferenceDecl => new ReferenceSlot(
                name,
                Required(slot, "targetConceptKey", $"slot '{name}'"),
                Optional(slot, "allowOwnSubtree") == "true"),

            _ => throw new ConceptDefinitionException(
                $"Concept '{owner}' has a slot '{name}' of kind '{slot.Concept.Key}', which is not a slot kind."),
        };
    }

    /// <summary>The type a declared property names: one of the four built-ins, or a declared enumeration.</summary>
    private static PropertyType TypeNamed(
        string typeName,
        string owner,
        string slot,
        Dictionary<string, IReadOnlyList<string>> enums)
    {
        return typeName switch
        {
            "string" => PropertyType.String,
            "int" => PropertyType.Integer,
            "bool" => PropertyType.Boolean,
            "date" => PropertyType.Date,
            _ => enums.TryGetValue(typeName, out var symbols)
                ? PropertyType.Enum([.. symbols])
                : throw new ConceptDefinitionException(
                    $"Property '{slot}' of concept '{owner}' names the type '{typeName}', which is neither a built-in (string, int, bool, date) nor a declared enumeration."),
        };
    }

    /// <summary>The text of a property the definition has to fill, or a definition error naming who left it out.</summary>
    private static string Required(Node node, string slot, string owner)
    {
        var value = node.GetOrNull(slot);

        return string.IsNullOrWhiteSpace(value)
            ? throw new ConceptDefinitionException($"'{owner}' does not say its '{slot}'.")
            : value;
    }

    private static string? Optional(Node node, string slot) =>
        node.GetOrNull(slot) is { Length: > 0 } value ? value : null;

    private static bool Flag(Node node, string slot) => node.GetOrNull(slot) == "true";
}
