using Projectional.Core;
using Projectional.ConstraintRules;
using Projectional.Editors;
using Projectional.Editors.Rendering;
using Projectional.Expressions;
using Projectional.IntentionRules;
using Projectional.IntentionRules.Notation;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Samples;
using Projectional.ScopeRules;
using Projectional.ScopeRules.Notation;
using Projectional.Structures;
using Projectional.Structures.Notation;
using Projectional.TextGen;
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
    /// <summary>The concepts the sample rules language declares, which is what makes one of its documents runnable.</summary>
    private static readonly HashSet<string> SampleConceptKeys =
        [.. RulesLanguage.ConceptDeclarations.Select(declaration => declaration.Key)];

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
            .. TextGenLanguage.ConceptDeclarations,
            .. ConstraintLanguage.ConceptDeclarations,
            .. ScopeLanguage.ConceptDeclarations,
            .. IntentionLanguage.ConceptDeclarations,
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

    /// <summary>The text generators of the language <paramref name="model"/> is written in.</summary>
    /// <param name="model">The model, whose first root decides which language it belongs to.</param>
    /// <returns>The generators, or an empty list when the language declares none.</returns>
    /// <remarks>
    /// A language declares as many generators as it has targets, so this answers a list and the shell lets the user
    /// pick one - there is no "the" generator. Which generators a document has is a fact about its language, exactly
    /// like whether it can be run, so it is derived from the root's concept rather than from the command that opened
    /// the document.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyList<TextGenerator> GeneratorsFor(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Roots.Count > 0 && SampleConceptKeys.Contains(model.Roots[0].Concept.Key)
            ? RulesTextGenerators.All
            : [];
    }

    /// <summary>A model of the structure language: one empty concept, ready to be written.</summary>
    /// <remarks>
    /// One definition per document, so the menu entry creates a concept and nothing else: an enumeration is a
    /// document of its own, because the two are edited at different times and a diff should read as one definition.
    /// </remarks>
    internal Model NewConceptDefinition() => NewDefinition(StructureConceptKeys.Concept, "concept");

    /// <summary>A model of the structure language: one empty enumeration, ready to be written.</summary>
    internal Model NewEnumerationDefinition() => NewDefinition(StructureConceptKeys.Enumeration, "enumeration");

    /// <summary>A model of the editor language: one empty notation, ready to be written.</summary>
    internal Model NewNotationDefinition() => NewDefinition(EditorConceptKeys.Notation, "notation");

    /// <summary>A model of the generator language: one empty generator, ready to be written.</summary>
    internal Model NewGeneratorDefinition() => NewDefinition(TextGenConceptKeys.Generator, "generator");

    /// <summary>A model of the constraint language: one empty rule, ready to be written.</summary>
    internal Model NewConstraintDefinition() => NewDefinition(ConstraintConceptKeys.Rule, "constraint");

    /// <summary>A model holding exactly one new root of <paramref name="conceptKey"/>.</summary>
    private Model NewDefinition(string conceptKey, string name)
    {
        var model = NewProject().NewModel(name);

        model.AddRoot(model.NewNode(conceptKey));

        return model;
    }

    /// <summary>The interpreter of the language <paramref name="model"/> is written in, or <see langword="null"/>.</summary>
    /// <param name="model">The model, whose first root decides which language it belongs to.</param>
    /// <returns>The rules that give its nodes a meaning, or <see langword="null"/> when the language defines none.</returns>
    /// <remarks>
    /// <para>
    /// A language says what its concepts mean, so "can this document be run" is answered by the concept its root is -
    /// not by the menu entry that opened it. The shell ships an interpreter for the sample rules language and for
    /// nothing else, so a document of a language opened from a folder, of either definition language or of the type
    /// rule language is simply not runnable.
    /// </para>
    /// <para>
    /// Answering <see langword="null"/> rather than falling back to the sample's rules is the whole point: running a
    /// structure definition against them would report a missing rule for every concept, which is a wrong answer to a
    /// question nobody should have been able to ask.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    internal static EvaluationRegistry? InterpreterFor(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Roots.Count > 0 && SampleConceptKeys.Contains(model.Roots[0].Concept.Key)
            ? Evaluation
            : null;
    }

    /// <summary>The sample language's interpreter, which is the only one the shell ships.</summary>
    /// <remarks>
    /// <para>
    /// A document is runnable when its language defines an interpreter, so this registry is what makes the sample
    /// rules language runnable and everything else - the two definition languages, a type ruleset, a document of a
    /// language opened from a folder - not: evaluation is an aspect a language declares, and none of them declares
    /// one yet.
    /// </para>
    /// <para>
    /// It composes the expression rules, so a rule's condition runs without the shell bolting two registries
    /// together.
    /// </para>
    /// </remarks>
    internal static EvaluationRegistry Evaluation => RulesEvaluation.Registry;

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
                // The framework's own interfaces are injected by Create, so re-declaring them would be
                // refused as a reserved key - and re-declaring is only ever about the languages.
                .. Concepts.Concepts
                    .Where(concept => !FrameworkInterfaces.All.Any(framework => framework.Key == concept.Key))
                    .Select(Redeclare),
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
        TextGenNotations.AddTo(notations);
        ConstraintNotations.AddTo(notations);
        ScopeNotations.AddTo(notations);
        IntentionNotations.AddTo(notations);

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

        // Extensions, not SuperConcepts: the latter is the union of what this concept extends and what it
        // implements, and writing it back into the extension list would name each interface twice.
        concept.Extensions.Select(super => super.Key).ToArray(),
        concept.NameSlot?.Name,
        concept.IsAbstract,
        concept.IsOfferedInCompletion,
        concept.Description,
        concept.CollapseSlot?.Name,
        concept.Applicability,
        concept.Triggers,
        concept.IsInterface,
        concept.Interfaces.Select(implemented => implemented.Key).ToArray());

    private static SlotDeclaration Redeclare(SlotDeclaration slot) => slot switch
    {
        PropertySlot property => new PropertySlot(property.Name, property.Type, property.DefaultValue, property.Description),
        ChildSlot child => new ChildSlot(child.Name, child.ChildConceptKey, child.Cardinality, child.Minimum),
        ReferenceSlot reference => new ReferenceSlot(reference.Name, reference.TargetConceptKey, reference.AllowOwnSubtree),
        _ => throw new InvalidOperationException($"'{slot.GetType().Name}' is not a slot kind this shell can re-declare."),
    };
}
