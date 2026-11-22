using Projectional.Core;
using Projectional.CSharp;
using Projectional.CSharp.Checks;
using Projectional.CSharp.Generation;
using Projectional.CSharp.Intentions;
using Projectional.CSharp.Notation;
using Projectional.CSharp.Scopes;
using Projectional.Expressions.Intentions;
using Projectional.Semantics;
using Projectional.CSharp.Types;
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
using Projectional.TypingRules;
using Projectional.TypingRules.Notation;
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
internal sealed class WorkbenchLanguage : ILanguageResolver
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
        .. CSharpLanguage.ConceptDeclarations,
        .. ExpressionsLanguage.ConceptDeclarations,
        .. RulesLanguage.ConceptDeclarations,
        .. StructureLanguage.ConceptDeclarations,
        .. EditorLanguage.ConceptDeclarations,
        .. TypeRuleLanguage.ConceptDeclarations,
        .. TextGenLanguage.ConceptDeclarations,
        .. ConstraintLanguage.ConceptDeclarations,
        .. ScopeLanguage.ConceptDeclarations,

        // The typing language too, for the reason the importer language is here and with the same consequence: a
        // language project's typing/ folder is written in it, so a shell that does not declare it cannot open the
        // rules an author writes to say what a burst of typing creates.
        .. TypingLanguage.ConceptDeclarations,

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

    /// <summary>The languages the shell ships, which a declared project may name as a base.</summary>
    /// <remarks>
    /// The second kind of answer <see cref="Find"/> gives. A base language is always available - that is what "base"
    /// means, and it is why C# is here rather than in a folder a user has to open first - so a declared DSL writes
    /// <c>depends on CSharp</c> and gets the vocabulary without the shell having to be told where C# lives.
    /// </remarks>
    private static class ShippedLanguages
    {
        private static readonly Dictionary<string, LanguageProject> All = new(StringComparer.Ordinal)
        {
            [CSharpLanguage.Name] = LanguageProject.Shipped(
                CSharpLanguage.Name,
                version: null,
                CSharpLanguage.ConceptDeclarations,
                CSharpLanguage.Concepts,
                CSharpNotations.CreateNotations(CSharpLanguage.Concepts),
                CSharpConstraints.All,
                CSharpIntentions.Provider,
                [CSharpTextGenerators.Generator],
                scopes: CSharpScopes.Registry,
                types: CSharpTypes.Registry,
                checks: CSharpChecks.All,
                hasIntentions: true),

            // The expression sub-language ships for the same reason C# does: a DSL declares a slot over
            // `Expressions.Expression` and gets the vocabulary, so a reader of a document may meet its concepts without
            // having opened any language. Documenting it is what makes `Function`, `Call` and the rest findable.
            [ExpressionsLanguage.Name] = LanguageProject.Shipped(
                ExpressionsLanguage.Name,
                version: null,
                ExpressionsLanguage.ConceptDeclarations,
                ExpressionsLanguage.Concepts,
                ExpressionsLanguage.CreateNotations(ExpressionsLanguage.Concepts),
                ExpressionsLanguage.Constraints,
                ExpressionsIntentions.Provider,
                [],
                scopes: ExpressionsLanguage.Scopes,
                types: ExpressionsLanguage.Types,
                typing: ExpressionsLanguage.TypingRules,
                hasTyping: true,
                hasIntentions: true),
        };

        /// <summary>Every language the shell ships, which is what a documentation set starts from.</summary>
        /// <remarks>
        /// A documentation set documents what the repository publishes rather than what a user happened to open, and C#
        /// is the only language that is always there - so the generator needs the list, not just the lookup a resolver
        /// uses.
        /// </remarks>
        internal static IReadOnlyList<LanguageProject> Everything => [.. All.Values];

        internal static LanguageProject? Find(string name) =>
            All.TryGetValue(name, out var language) ? language : null;
    }

    /// <summary>The languages the shell ships, in the order their names sort.</summary>
    internal static IReadOnlyList<LanguageProject> Shipped =>
    [
        .. ShippedLanguages.Everything.OrderBy(language => language.Name, StringComparer.Ordinal),
    ];

    /// <summary>The typing rules the shell ships, which are what a language without a <c>typing</c> aspect gets.</summary>
    internal static ITypingRules? Typing => RulesLanguage.TypingRules;

    /// <summary>The scopes a document of the shell own languages resolves its references with.</summary>
    /// <remarks>
    /// Combined rather than chosen, because one document can hold concepts of several of them: a rule condition is an
    /// expression, an expression can hold a C# call, and a call names a function of its project. A language that brings a
    /// scope of its own still wins, because a document opened from a project passes that registry instead.
    /// </remarks>
    internal static ScopeProviderRegistry Scopes { get; } = ScopeProviderRegistry.Empty
        .With(CSharpScopes.Registry)
        .With(Projectional.Expressions.Scopes.ExpressionsScopes.Registry);

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

    /// <summary>The kind of definition document <c>File ▸ New ▸ Language definition ▸ Concept</c> starts.</summary>
    internal static DefinitionKind ConceptKind { get; } = new(
        "concept", "Concept", LanguageProject.StructureFolder, StructureConceptKeys.Concept);

    /// <summary>An enumeration, which is the other half of what a structure document can hold.</summary>
    internal static DefinitionKind EnumerationKind { get; } = new(
        "enumeration", "Enumeration", LanguageProject.StructureFolder, StructureConceptKeys.Enumeration);

    /// <summary>One concept's notation, which is one half of a language definition.</summary>
    internal static DefinitionKind NotationKind { get; } = new(
        "notation", "Notation", LanguageProject.EditorFolder, EditorConceptKeys.Notation);

    /// <summary>One type rule, which is what a <c>types</c> folder is written of.</summary>
    internal static DefinitionKind TypeRuleKind { get; } = new(
        "type-rule", "Type rule", LanguageProject.TypesFolder, TypeRuleConceptKeys.Rule);

    /// <summary>One declared type, which is what a rule that names a type of its own refers to.</summary>
    /// <remarks>
    /// The second of the five documents a <c>types</c> folder can hold, and the one the sample found missing: primitive
    /// names are <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c> and <c>date</c>, so a language whose nodes have a
    /// type of their own has to declare it - and a kind the shell could open but not start would make that impossible.
    /// </remarks>
    internal static DefinitionKind TypeNameKind { get; } = new(
        "type-name", "Named type", LanguageProject.TypesFolder, TypeRuleConceptKeys.Named);

    /// <summary>One rule the language states about its own documents.</summary>
    internal static DefinitionKind ConstraintKind { get; } = new(
        "constraint", "Constraint", LanguageProject.ConstraintsFolder, ConstraintConceptKeys.Rule);

    /// <summary>One rule saying where a reference slot's names come from.</summary>
    internal static DefinitionKind ScopeRuleKind { get; } = new(
        "scope-rule", "Scope rule", LanguageProject.ScopesFolder, ScopeConceptKeys.Rule);

    /// <summary>One rule saying which node a burst of typing creates.</summary>
    internal static DefinitionKind TypingRuleKind { get; } = new(
        "typing-rule", "Typing rule", LanguageProject.TypingFolder, TypingConceptKeys.Rule);

    /// <summary>One entry a language offers at a caret.</summary>
    internal static DefinitionKind IntentionKind { get; } = new(
        "intention", "Intention", LanguageProject.IntentionsFolder, IntentionConceptKeys.Rule);

    /// <summary>One way of printing the language's models as text.</summary>
    internal static DefinitionKind GeneratorKind { get; } = new(
        "generator", "Generator", LanguageProject.GeneratorFolder, TextGenConceptKeys.Generator);

    /// <summary>One way of reading a text as the language's models.</summary>
    internal static DefinitionKind ImporterKind { get; } = new(
        "importer", "Importer", LanguageProject.ImportFolder, ImportConceptKeys.Importer);

    /// <summary>Every definition document the shell can start, in the order the menu offers them.</summary>
    /// <remarks>
    /// <para>
    /// In the order of <see cref="LanguageProject.AspectFolders"/>: what a language declares, how it is drawn, and the
    /// aspects that give it a meaning. A kind whose folder is not in that list would be a document nothing opens, so a
    /// test holds the two together rather than trusting the two to stay in step.
    /// </para>
    /// <para>
    /// A <c>types</c> folder holds five kinds of document - a rule, a named type, supertypes, comparability and a
    /// replacement - and this list starts the first two: a rule says what a concept's type is, and a named type is what
    /// a rule of a language with types of its own has to refer to, so both can be created from nothing. The other
    /// three are statements about a type some rule already names, which waits until a language needs them. An
    /// <c>accessory</c> document is deliberately absent: it is not a definition but an ordinary model of the language,
    /// so it needs a language before it can be written at all.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<DefinitionKind> Definitions { get; } =
    [
        ConceptKind,
        EnumerationKind,
        NotationKind,
        TypeRuleKind,
        TypeNameKind,
        ConstraintKind,
        ScopeRuleKind,
        TypingRuleKind,
        IntentionKind,
        GeneratorKind,
        ImporterKind,
    ];

    /// <summary>A model of the language <paramref name="definition"/> is written in: one empty definition to write.</summary>
    /// <remarks>
    /// The root is created rather than left to the user, because a definition document <em>is</em> one definition: the
    /// sections of a concept, an importer's token classes, the mappings of a generator are cells of that root, so a
    /// document without one would have nowhere to type.
    /// </remarks>
    internal Model NewDefinition(DefinitionKind definition) => NewDefinition(definition.ConceptKey, definition.Id);

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

    /// <summary>The opened language called <paramref name="name"/>, which answers a declared dependency.</summary>
    /// <param name="name">The name a <c>language.mps</c> declared.</param>
    /// <remarks>
    /// The window's own set is the whole answer on purpose ([ADR 0026](../../docs/adr/0026-a-dependency-resolves-to-the-vocabulary.md)):
    /// nothing is read from beside the folder, so a language that stands on another asks the user to open that one
    /// first. The comparison is ordinal, like every other key in a model.
    /// </remarks>
    public LanguageProject? Find(string name) =>
        _opened.FirstOrDefault(opened => string.Equals(opened.Name, name, StringComparison.Ordinal))
        ?? ShippedLanguages.Find(name);

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
    /// A concept can be a root when it is concrete, names no applicability places and does not refuse to start a
    /// document: a concept that names places can never be a root, because it needs a parent and a slot to exist, and
    /// one that declared <c>isRoot: false</c> said so itself. The first such concept in declaration order is the
    /// language author's answer to "what is this language about", the same order that ranks completion.
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
    /// <remarks>
    /// <para>
    /// Three conditions, and the third one is the one that matters: the concept must be concrete, must name no
    /// applicability places (so it needs no parent), and must not have refused to start a document. Without the third
    /// this method and <see cref="Model.NewNode(Concept)"/> disagree, and the disagreement is not a wrong answer but a
    /// crash: the chooser offers a concept the model then refuses to make a root.
    /// </para>
    /// <para>
    /// The C# base language is where that was found. It declares <c>isRoot: false</c> on 44 of its 45 concepts, so
    /// "the first concrete one that names no places" was <c>CSharp.TypeReference</c> - and opening a new C# document
    /// threw <see cref="ModelInvariantException"/> instead of starting a file. Only <c>CSharp.File</c> means "a
    /// document", and only asking about the flag says so.
    /// </para>
    /// </remarks>
    internal static Concept? Rootable(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return language.Concepts.Concepts.FirstOrDefault(
            concept => !concept.IsAbstract && concept.Applicability.IsAnywhere && concept.IsRoot);
    }

    /// <summary>The notation of the base languages plus everything opened, against <paramref name="concepts"/>.</summary>
    private static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        var notations = RulesLanguage.CreateNotations(concepts);

        CSharpNotations.AddTo(notations);

        StructureNotations.AddTo(notations);
        EditorNotations.AddTo(notations);
        TypeRuleNotations.AddTo(notations);
        TextGenNotations.AddTo(notations);
        ConstraintNotations.AddTo(notations);
        ScopeNotations.AddTo(notations);
        TypingNotations.AddTo(notations);
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
