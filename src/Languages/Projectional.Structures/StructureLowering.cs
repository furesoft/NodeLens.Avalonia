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
    /// A root is neither a concept nor an enumeration (a language root belongs at the project root, not here), a
    /// concept or enumeration key is declared twice, a property names no type or an unknown one, an enumeration
    /// holds no symbols, or a child or reference slot names no concept.
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

                case StructureConceptKeys.Language:
                    // The root is not a definition and cannot be one: it belongs at the project root as
                    // `language.mps`, and a copy under `structure/` would otherwise be silently ignored.
                    throw new ConceptDefinitionException(
                        "A language root belongs at the project root as 'language.mps', not in a structure document; a folder's structure holds one concept or one enumeration per document.");

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

    /// <summary>What a language root says about its language.</summary>
    /// <param name="root">The one root of <c>language.mps</c>.</param>
    /// <returns>The name, version and the two lists the root declares.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">
    /// The root is not a <see cref="StructureConceptKeys.Language"/>, it does not say its name, a dependency does
    /// not say its name, or the same name appears twice in one of the two lists.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The entry point of the one document that is not a definition. It is separate from <see cref="Lower"/> on
    /// purpose: a root describes the project, while a definition describes a concept, and asking one method to
    /// answer both would be the container concept the layout is built to avoid.
    /// </para>
    /// <para>
    /// Nothing here resolves a dependency. It checks only that the root is readable - a name where one is needed and
    /// no name declared twice - because the graph behind the names is a project boundary, which is a decision of its
    /// own.
    /// </para>
    /// </remarks>
    public static LanguageRoot ReadLanguage(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Concept.Key != StructureConceptKeys.Language)
        {
            throw new ConceptDefinitionException(
                $"'{root.Concept.Key}' is not a language root; 'language.mps' holds one '{StructureConceptKeys.Language}'.");
        }

        var name = Required(root, "name", "the language root");

        return new LanguageRoot(
            name,
            Optional(root, StructureLanguage.VersionSlot),
            Dependencies(root, StructureLanguage.DependenciesSlot, name),
            Dependencies(root, StructureLanguage.ExtendsSlot, name));
    }

    /// <summary>One of the root's two lists, read in the order the root names it.</summary>
    private static List<LanguageDependency> Dependencies(Node root, string slot, string language)
    {
        var dependencies = new List<LanguageDependency>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var child in root.GetChildren(slot))
        {
            var name = Optional(child, "name")
                ?? throw new ConceptDefinitionException(
                    $"Language '{language}' declares an entry in '{slot}' without a name.");

            if (!names.Add(name))
            {
                throw new ConceptDefinitionException(
                    $"Language '{language}' names '{name}' twice in '{slot}'; a dependency is declared once.");
            }

            dependencies.Add(new LanguageDependency(name, Optional(child, StructureLanguage.VersionSlot)));
        }

        return dependencies;
    }

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
        var isAbstract = Flag(concept, StructureLanguage.IsAbstractProperty);
        var isInterface = Flag(concept, StructureLanguage.IsInterfaceProperty);

        if (isInterface && !isAbstract)
        {
            // The registry refuses this too, but a definition that says both is a mistake in the document the
            // author is looking at, so it is reported where it was written.
            throw new ConceptDefinitionException(
                $"Concept '{key}' is an interface but not abstract; an interface is abstract by definition.");
        }

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
            isAbstract: isAbstract,
            isOfferedInCompletion: Flag(concept, "isOfferedInCompletion"),
            description: Optional(concept, "description"),            collapseSlot: Optional(concept, "collapseSlot"),
            // An empty cell means "inherit", not "any": the declaration stays null and the registry walks the
            // supers. A word the enumeration does not offer cannot reach here, because the model refuses it.
            nameRule: Optional(concept, "nameRule") is { } rule ? NameRule.Parse(rule) : null,
            isInterface: isInterface,
            interfaces: concept.GetChildren(StructureLanguage.ImplementsSlot)
                .Select(implemented => Required(implemented, "conceptKey", $"'{key}'")),
            alias: Optional(concept, StructureLanguage.AliasProperty),

            // Absent means "true": a definition written before the flag existed allowed every concept to start a
            // document, and reading one has to keep meaning that.
            isRoot: !string.Equals(
                concept.GetOrNull(StructureLanguage.IsRootProperty),
                "false",
                StringComparison.Ordinal));
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

            StructureConceptKeys.ChildDecl => LowerChildSlot(name, slot),

            StructureConceptKeys.ReferenceDecl => new ReferenceSlot(
                name,
                Required(slot, "targetConceptKey", $"slot '{name}'"),
                Optional(slot, "allowOwnSubtree") == "true"),

            _ => throw new ConceptDefinitionException(
                $"Concept '{owner}' has a slot '{name}' of kind '{slot.Concept.Key}', which is not a slot kind."),
        };
    }

    /// <summary>The child slot a declaration stands for, with the range its cardinality spells.</summary>
    /// <remarks>
    /// The four spellings are the ones the notation offers in one cell - <c>0..1</c>, <c>0..n</c>, <c>1</c>,
    /// <c>1..n</c> - and the range is what the model stores: the storage shape in <see cref="Cardinality"/> and the
    /// lower end in <see cref="ChildSlot.Minimum"/>. A slot that says nothing is <c>1</c>, which is what every
    /// child slot meant before the range was written down.
    /// </remarks>
    private static ChildSlot LowerChildSlot(string name, Node slot)
    {
        var cardinality = Optional(slot, "cardinality") ?? "1";

        var (kind, minimum) = cardinality switch
        {
            "0..1" => (Cardinality.One, 0),
            "0..n" => (Cardinality.Many, 0),
            "1" => (Cardinality.One, 1),
            "1..n" => (Cardinality.Many, 1),
            _ => throw new ConceptDefinitionException(
                $"Slot '{name}' declares the cardinality '{cardinality}', which is not one of 0..1, 0..n, 1 or 1..n."),
        };

        return new ChildSlot(name, Required(slot, "childConceptKey", $"slot '{name}'"), kind, minimum);
    }

    /// <summary>The type a declared property names: one of the five built-ins, or a declared enumeration.</summary>
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
            "float" => PropertyType.Float,
            "bool" => PropertyType.Boolean,
            "date" => PropertyType.Date,
            _ => enums.TryGetValue(typeName, out var symbols)
                ? PropertyType.Enum([.. symbols])
                : throw new ConceptDefinitionException(
                    $"Property '{slot}' of concept '{owner}' names the type '{typeName}', which is neither a built-in (string, int, float, bool, date) nor a declared enumeration."),
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
