using Projectional.Core;
using Projectional.Editors;
using Projectional.Editors.Rendering;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Samples;
using Projectional.Structures;
using Projectional.Structures.Notation;
using Projectional.TypeRules;

namespace Projectional.Workbench;

/// <summary>
/// The languages one shell instance knows: the sample rules language, the two the editor is written with, and any
/// language project the user opened.
/// </summary>
/// <remarks>
/// <para>
/// An instance rather than a static table, because a shell is a library: two shells in one process must not see each
/// other's languages ([ADR 0015](../../docs/adr/0015-shell-is-a-library.md)), and a language loaded from a folder has
/// to join the set the shell already has. Growing the set replaces both registries wholesale rather than mutating
/// one, so a document opened earlier keeps drawing with the registry it was built against.
/// </para>
/// <para>
/// Composition, not inheritance, is the pattern [ADR 0014](../../docs/adr/0014-both-authoring-paths-are-permanent.md)
/// prescribes: every language is borrowed with <c>[.. TheirLanguage.ConceptDeclarations, ..]</c>. Note that a
/// language's <c>ConceptDeclarations</c> is only its *own* concepts - the rules language borrows
/// <see cref="ExpressionsLanguage"/>, so a host composing it has to bring that too.
/// </para>
/// </remarks>
internal sealed class WorkbenchLanguage
{
    private readonly List<LanguageProject> _opened = [];

    /// <summary>Builds the set every shell starts with.</summary>
    internal WorkbenchLanguage()
    {
        Concepts = ConceptRegistry.Create(
        [
            .. ExpressionsLanguage.ConceptDeclarations,
            .. RulesLanguage.ConceptDeclarations,
            .. StructureLanguage.ConceptDeclarations,
            .. EditorLanguage.ConceptDeclarations,
            .. TypeRuleLanguage.ConceptDeclarations,
        ]);

        Notations = CreateNotations(Concepts);
    }

    /// <summary>The registry every document of this shell is built and loaded against.</summary>
    internal ConceptRegistry Concepts { get; private set; }

    /// <summary>The notation of every language in the set, in one registry.</summary>
    internal NotationRegistry Notations { get; private set; }

    /// <summary>The sample language's emphasis rules, which are the only ones the shell ships.</summary>
    internal static ICellEmphasis? Emphasis => RulesLanguage.Emphasis;

    /// <summary>The sample language's typing rules, which are the only ones the shell ships.</summary>
    internal static ITypingRules? Typing => RulesLanguage.TypingRules;

    /// <summary>The languages opened from a folder, in the order they joined.</summary>
    internal IReadOnlyList<LanguageProject> Opened => _opened;

    /// <summary>A project every document of this shell can be loaded into.</summary>
    internal Project NewProject() => new(Concepts);

    /// <summary>Whether the shell already knows <paramref name="language"/>.</summary>
    /// <param name="language">The language to look for.</param>
    internal bool Contains(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return _opened.Any(opened => string.Equals(opened.Directory, language.Directory, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Takes <paramref name="language"/> into the set, or refuses it with a reason.</summary>
    /// <param name="language">The loaded language project.</param>
    /// <param name="error">Why it could not join, or <see langword="null"/> on success.</param>
    /// <returns>Whether the shell can now open documents of that language.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="language"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The candidate registries are built first and only adopted if both succeed, so a language that clashes leaves
    /// the shell exactly as it was. A half-added language would give a document a concept registry that disagrees
    /// with the notation registry in force, which is the one state that produces files nobody can open.
    /// </remarks>
    internal bool TryAdd(LanguageProject language, out string? error)
    {
        ArgumentNullException.ThrowIfNull(language);

        ConceptRegistry concepts;
        NotationRegistry notations;

        try
        {
            concepts = ConceptRegistry.Create(
            [
                .. Concepts.Concepts.Select(Redeclare),
                .. language.Declarations,
            ]);

            notations = CreateNotations(concepts);

            foreach (var opened in _opened)
            {
                AddNotations(notations, opened.Notations);
            }

            AddNotations(notations, language.Notations);
        }
        catch (Exception exception) when (exception is ConceptDefinitionException or NotationException)
        {
            error = exception.Message;

            return false;
        }

        _opened.RemoveAll(opened => string.Equals(opened.Directory, language.Directory, StringComparison.OrdinalIgnoreCase));
        _opened.Add(language);
        Concepts = concepts;
        Notations = notations;
        error = null;

        return true;
    }

    /// <summary>A model of the structure language: an empty language definition, ready to be written.</summary>
    internal Model NewLanguageDefinition()
    {
        var model = NewProject().NewModel("structure");
        var definition = model.NewNode(StructureConceptKeys.Language);

        model.AddRoot(definition);

        return model;
    }

    /// <summary>A model of the editor language: an empty notation, ready to be written.</summary>
    internal Model NewNotationDefinition()
    {
        var model = NewProject().NewModel("editor");
        var notations = model.NewNode(EditorConceptKeys.Notations);

        model.AddRoot(notations);

        return model;
    }

    /// <summary>A document of <paramref name="language"/>, or <see langword="null"/> when it has no root concept.</summary>
    /// <param name="language">The language to create a document of.</param>
    /// <remarks>
    /// A concept can be a root when it is concrete and names no applicability places: a concept that names places
    /// can never be a root, because it needs a parent and a slot to exist. The first such concept in declaration
    /// order is the language author's answer to "what is this language about", the same order that ranks completion.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="language"/> is <see langword="null"/>.</exception>
    internal static Model? NewDocument(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (Rootable(language) is not { } concept)
        {
            return null;
        }

        var model = language.NewModel(concept.Key);
        var root = model.NewNode(concept.Key);

        model.AddRoot(root);

        return model;
    }

    /// <summary>The concept a document of <paramref name="language"/> could start with, or <see langword="null"/>.</summary>
    internal static Concept? Rootable(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return language.Concepts.Concepts.FirstOrDefault(concept => !concept.IsAbstract && concept.Applicability.IsAnywhere);
    }

    /// <summary>The notation of the base languages plus everything opened, against <paramref name="concepts"/>.</summary>
    private static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        var notations = RulesLanguage.CreateNotations(concepts);

        StructureNotations.AddTo(notations);
        EditorNotations.AddTo(notations);
        TypeRuleNotations.AddTo(notations);

        return notations;
    }

    private static void AddNotations(NotationRegistry notations, NotationRegistry source)
    {
        foreach (var notation in source.Notations)
        {
            notations.Add(notation);
        }
    }

    /// <summary>A concept re-declared, so the registry can be rebuilt with one more language.</summary>
    /// <remarks>
    /// <see cref="ConceptRegistry"/> is immutable and takes declarations, not concepts, so growing the set means
    /// writing every concept down again. The information is the same one the declaration carried, in the same order -
    /// which is also why the dump comparison of the parity harness covers this path.
    /// </remarks>
    private static ConceptDeclaration Redeclare(Concept concept) => new(
        concept.Key,
        concept.Slots.Select(Redeclare).ToArray(),
        concept.SuperConcepts.Select(super => super.Key).ToArray(),
        concept.NameSlot?.Name,
        concept.IsAbstract,
        concept.IsOfferedInCompletion,
        concept.Description,
        concept.CollapseSlot?.Name,
        concept.Applicability,
        concept.Triggers);

    private static SlotDeclaration Redeclare(SlotDeclaration slot) => slot switch
    {
        PropertySlot property => new PropertySlot(property.Name, property.Type, property.DefaultValue, property.Description),
        ChildSlot child => new ChildSlot(child.Name, child.ChildConceptKey, child.Cardinality),
        ReferenceSlot reference => new ReferenceSlot(reference.Name, reference.TargetConceptKey, reference.AllowOwnSubtree),
        _ => throw new InvalidOperationException($"'{slot.GetType().Name}' is not a slot kind this shell can re-declare."),
    };
}
