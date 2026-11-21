using Projectional.Core;
using Projectional.ConstraintRules;
using Projectional.Editors;
using Projectional.Editors.Rendering;
using Projectional.Expressions;
using Projectional.ImportRules;
using Projectional.IntentionRules;
using Projectional.IntentionRules.Notation;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Samples;
using Projectional.ScopeRules;
using Projectional.ScopeRules.Notation;
using Projectional.Structures;
using Projectional.Structures.Notation;
using Projectional.Testing;
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

    /// <summary>
    /// What the shell declares on its own, which every set is composed from.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Concepts"/> because a language opened from a folder can be opened again - that is
    /// how an edited importer, constraint or notation takes effect - and a set built from the composed registry would
    /// re-declare the language it is replacing. Rebuilding from these plus the languages that stay makes re-opening a
    /// replacement instead of a second declaration of the same keys.
    /// </remarks>
    private static readonly IReadOnlyList<ConceptDeclaration> ShellDeclarations =
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

        // The test language too: a language project's tests/ folder holds documents of it, and a shell that cannot
        // load them refuses the very files the tests command runs - the failure read "Testing.Test, which the
        // language does not declare".
        .. TestingLanguage.ConceptDeclarations,

        // And the importer language, for the same reason and with the same consequence: a language's import/
        // folder is written in it, so a shell that does not declare it cannot open the file an author writes to
        // teach the shell a format.
        .. ImportLanguage.ConceptDeclarations,
    ];

    /// <summary>Builds the set every shell starts with.</summary>
    internal WorkbenchLanguage()
    {
        Concepts = ConceptRegistry.Create(ShellDeclarations);
        Notations = CreateNotations(Concepts);
    }

    /// <summary>The registry every document of this shell is built and loaded against.</summary>
    internal ConceptRegistry Concepts { get; private set; }

    /// <summary>The notation of every language in the set, in one registry.</summary>
    internal NotationRegistry Notations { get; private set; }

    /// <summary>The sample language's emphasis rules, which are the only ones the shell ships.</summary>
    internal static ICellEmphasis? Emphasis => RulesLanguage.Emphasis;

    /// <summary>The typing rules the shell ships, which are what a language without a <c>typing</c> aspect gets.</summary>
    internal static ITypingRules? Typing => RulesLanguage.TypingRules;

    /// <summary>The typing rules a document of <paramref name="language"/> claims typed text with.</summary>
    /// <param name="language">The language the document is written in.</param>
    /// <returns>That language's own rules, or the shell's when it declares none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="language"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The <c>typing</c> aspect of a language project is where a declared language says what a burst of typing creates,
    /// so a document of an opened language asks <em>that</em> language - the aspect is part of the language for the
    /// same reason its types and constraints are, and a document that claimed text by the shell's rules would be a
    /// document of a language nobody wrote.
    /// <para>
    /// A language that declares none keeps the shell's set, which is what every language without the aspect has always
    /// been given. That is deliberately a fallback and not <see cref="TypingRuleRegistry.Empty"/>: the two answer the
    /// same text the same way for a declared language (the shell's rules name <c>Expressions</c> concepts no declared
    /// language has), but a language that composes those concepts still gets them, exactly as it did before the aspect
    /// existed.
    /// </para>
    /// </remarks>
    internal static ITypingRules? TypingFor(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return language.HasTyping ? language.Typing : Typing;
    }

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

    /// <summary>
    /// A model of the importer language: one empty importer, ready to be written.
    /// </summary>
    /// <remarks>
    /// The root is created rather than left to the user because an importer document <em>is</em> one importer, the
    /// same way a structure document is one concept: the sections for its token classes and its rules are cells of
    /// that root, so a document without one would have nowhere to type.
    /// </remarks>
    internal Model NewImportDefinition() => NewDefinition(ImportConceptKeys.Importer, "import");

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

    /// <summary>Reads <paramref name="text"/> with the importers of the opened languages, in the order they joined.</summary>
    /// <param name="name">The document name to give the model the text becomes.</param>
    /// <param name="text">The file's text, as the shell read it.</param>
    /// <param name="reason">Why nothing read it, or <see langword="null"/> when a language read it or none was asked.</param>
    /// <returns>The document, the language that read it and what it answered, or <see langword="null"/>.</returns>
    /// <remarks>
    /// <para>
    /// Every importer is tried, each into a model of its own, and the first that reads the text keeps it: a failed
    /// import leaves its model untouched, so nothing is shared between attempts and the order of the list is the
    /// order of the answer. A declared importer comes before the one a generator derives, because
    /// <see cref="LanguageProject.Importers"/> builds the list that way - a language that wrote a rule for a
    /// spelling meant that rule.
    /// </para>
    /// <para>
    /// <see cref="ITextImporter.CanImport"/> narrows nothing here. It is a hint for a file dialog (a cheap shape
    /// check), and using it as a gate would refuse a file the rules can read: an importer whose root rule begins with
    /// an anchor answers "no" for a document that starts with a comment it declares as skipped, which is exactly a
    /// document the importer reads. The hint therefore decides only *which* refusal is worth repeating - the first
    /// importer that claimed the text, or the first one tried when none did.
    /// </para>
    /// </remarks>
    internal ImportedDocument? TryImport(string name, string text, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(text);

        (string Format, string Language, ImportDiagnostic Refusal)? claimed = null;
        (string Format, string Language, ImportDiagnostic Refusal)? attempted = null;

        foreach (var language in _opened)
        {
            foreach (var importer in language.Importers)
            {
                var model = language.NewProject().NewModel(name);
                var result = importer.Import(model, text);

                if (result.Succeeded)
                {
                    reason = null;

                    return new ImportedDocument(model, language, result);
                }

                if (result.Diagnostics.Count == 0)
                {
                    continue;
                }

                var refusal = (importer.Format, language.Name, result.Diagnostics[0]);

                attempted ??= refusal;

                if (claimed is null && importer.CanImport(text))
                {
                    claimed = refusal;
                }
            }
        }

        reason = (claimed ?? attempted) is { } stop
            ? $"'{stop.Format}' of '{stop.Language}' stopped at {stop.Refusal.Line}:{stop.Refusal.Column} - {stop.Refusal.Message}"
            : null;

        return null;
    }

    /// <summary>The opened language project <paramref name="model"/> is written in, or <see langword="null"/>.</summary>
    /// <param name="model">The model, whose first root decides which language it belongs to.</param>
    /// <remarks>
    /// <para>
    /// The question is answered by the root's concept rather than by the command that opened the document, for the
    /// same reason <see cref="InterpreterFor"/> is: which language a document is written in is a fact about the
    /// document. A root declared by no opened project - the shell's own sample or definition languages - has no
    /// project, which is the answer "there is no folder to run tests from".
    /// </para>
    /// <para>
    /// It is the root that decides, not every concept the document borrows: a document may hold concepts of several
    /// languages, but it was created as one thing, and that thing is what its tests folder belongs to.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    internal LanguageProject? ProjectFor(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Roots.Count == 0
            ? null
            : _opened.FirstOrDefault(project => project.Concepts.TryGet(model.Roots[0].Concept.Key, out _));
    }

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

        // The languages that stay are decided before the set is built, because the set is built *from* them: a
        // language re-opened from the same folder replaces its predecessor, and keeping the old declarations in the
        // composition would declare every one of its concepts twice.
        var survivors = _opened
            .Where(opened => !string.Equals(opened.Directory, language.Directory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        try
        {
            concepts = ConceptRegistry.Create(
            [
                // The framework's own interfaces are injected by Create, so re-declaring them would be
                // refused as a reserved key - and re-declaring is only ever about the languages. The shell's own
                // declarations are re-declared rather than reused through a registry, because a registry is keyed by
                // concept and the ones that stay below are files that may have changed since they were read.
                .. ShellDeclarations
                    .Where(declaration => !FrameworkInterfaces.All.Any(framework => framework.Key == declaration.Key)),
                .. survivors.SelectMany(opened => opened.Declarations),
                .. language.Declarations,
            ]);

            notations = CreateNotations(concepts);

            foreach (var opened in survivors)
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
        _opened.Add(language);        Concepts = concepts;
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
        TestingNotations.AddTo(notations);
        ImportNotations.AddTo(notations);

        return notations;
    }

    private static void AddNotations(NotationRegistry notations, NotationRegistry source)
    {
        foreach (var notation in source.Notations)
        {
            notations.Add(notation);
        }
    }
}
