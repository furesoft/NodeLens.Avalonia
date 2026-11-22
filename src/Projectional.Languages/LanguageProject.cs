using Projectional.Core;
using Projectional.Checks;
using Projectional.EvaluationRules;
using Projectional.ConstraintRules;
using Projectional.Editing;
using Projectional.IntentionRules;
using Projectional.Editors;
using Projectional.ImportRules;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.ScopeRules;
using Projectional.Semantics;
using Projectional.Structures;
using Projectional.TextGen;
using Projectional.TypingRules;
using Projectional.TypeRules;

namespace Projectional.Languages;

/// <summary>
/// A language that lives in a folder: its structure and its notation as project files, lowered into a registry a
/// session can draw with.
/// </summary>
/// <remarks>
/// <para>
/// This is the shape [language-authoring.md](../../docs/architecture/language-authoring.md) describes - one model
/// per aspect, in a directory named after the language - and the point where the declared path stops being a pair
/// of documents and becomes a language: two files become a <see cref="ConceptRegistry"/> and a
/// <see cref="NotationRegistry"/>, which is everything a session needs to open a document of it.
/// </para>
/// <para>
/// Loading is best effort, in the same sense a preview is: a host that opens a folder gets either a usable language
/// or a message saying which file and which concept is wrong, never an exception at the moment the user picks a
/// directory. The one thing it does not do is guess - a definition that names a concept it never declared is an
/// error, not a warning, because a language that half-loads produces documents nobody can open.
/// </para>
/// </remarks>
public sealed class LanguageProject
{
    /// <summary>
    /// The file at the project root that is the language itself: its name, its version and the languages it stands
    /// on.
    /// </summary>
    /// <remarks>
    /// The only document of a project that is not a definition, and the only one that does not live in an aspect
    /// folder. It is optional: a folder without it is still a language, named after the folder and declaring no
    /// version and no dependencies, which is what every project was before the file existed.
    /// </remarks>
    public const string LanguageFile = "language.mps";

    /// <summary>The folder holding the concepts and enumerations, which every language project needs.</summary>
    public const string StructureFolder = "structure";

    /// <summary>The folder holding the notation, which a language may leave out while it is being written.</summary>
    public const string EditorFolder = "editor";

    /// <summary>The folder holding the type rules and the named types, which a language may leave out.</summary>
    public const string TypesFolder = "types";

    /// <summary>The folder whose documents say what the concepts of this language evaluate to.</summary>
    public const string EvaluationFolder = "evaluation";

    /// <summary>The folder holding the text generators, which a language may leave out until it has any.</summary>
    public const string GeneratorFolder = "generator";

    /// <summary>The folder holding the language's own constraints, which a language may leave out.</summary>
    public const string ConstraintsFolder = "constraints";

    /// <summary>The folder holding the language's own scope rules, which a language may leave out.</summary>
    public const string ScopesFolder = "scopes";
    /// <summary>The folder holding the language's own typing rules, which a language may leave out.</summary>
    public const string TypingFolder = "typing";

    /// <summary>
    /// The folder holding the models the language ships for its users - a colour palette, standard units, a set of
    /// ready-made types - which a language may leave out.
    /// </summary>
    /// <remarks>
    /// Ordinary models of the language's own concepts, not definitions of it: every document of the language gets
    /// their nodes in its own project, so a user can point a reference at a palette entry and the language carries a
    /// library instead of only a vocabulary. MPS calls them accessory models.
    /// </remarks>
    public const string AccessoryFolder = "accessory";

    /// <summary>The folder holding the language's declared importers, which a language may leave out.</summary>
    public const string ImportFolder = "import";

    /// <summary>The folder holding the language's intentions, which a language may leave out.</summary>
    public const string IntentionsFolder = "intentions";

    /// <summary>The folders that hold a language's definition documents, in the order a reader meets them.</summary>
    /// <remarks>
    /// <para>
    /// One list, because two copies of it drifted: the project explorer had its own and so did the shell's "open a
    /// language" loop, and the second never learned about <see cref="GeneratorFolder"/> - a folder the language
    /// declares, the explorer shows and the shell therefore never opened.
    /// </para>
    /// <para>
    /// The order is the reading order of a language: what it declares, how it is drawn, the aspects that give it a
    /// meaning, and last the two whose documents say what a model becomes. The <c>tests</c> folder is not here and
    /// cannot be, because a language does not depend on its own tests - the test runner reads that one - and neither
    /// is <see cref="LanguageFile"/>, which sits beside the folders rather than inside one.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> AspectFolders { get; } =
    [
        StructureFolder,
        EditorFolder,
        TypesFolder,
        EvaluationFolder,
        ConstraintsFolder,
        ScopesFolder,
        TypingFolder,
        AccessoryFolder,
        IntentionsFolder,
        ImportFolder,
        GeneratorFolder,
    ];

    private LanguageProject(
        string directory,
        LanguageRoot? languageRoot,
        IReadOnlyList<LanguageProject> resolved,
        IReadOnlyList<LanguageProject> inherited,
        IReadOnlyList<ConceptDeclaration> declarations,
        ConceptRegistry concepts,
        NotationRegistry notations,
        TypeRegistry types,
        IReadOnlyList<TextGenerator> generators,
        ConstraintSet constraints,
        ScopeProviderRegistry scopes,
        ITypingRules typing,
        IReadOnlyList<DeclaredTypingRule> typingRules,
        bool hasTyping,
        IReadOnlyList<Model> accessory,
        IIntentionProvider intentions,
        bool hasIntentions,
        IReadOnlyList<ITextImporter> importers,
        CheckSet? checks,
        EvaluationRegistry? evaluation)
    {
        Name = languageRoot?.Name ?? new DirectoryInfo(directory).Name;
        Directory = directory;
        Version = languageRoot?.Version;
        Description = languageRoot?.Description;
        Dependencies = languageRoot?.Dependencies ?? [];
        Extends = languageRoot?.Extends ?? [];
        Resolved = resolved;
        Inherited = inherited;
        Declarations = declarations;
        Concepts = concepts;
        Notations = notations;
        Types = types;
        Generators = generators;
        Constraints = constraints;
        Scopes = scopes;
        Typing = typing;
        HasTyping = hasTyping;
        DeclaredTypingRules = typingRules;
        Accessory = accessory;
        Intentions = intentions;
        HasIntentions = hasIntentions;
        DeclaredImporters = importers;
        Checks = checks;
        Evaluation = evaluation;
    }

    /// <summary>The importers the <c>import</c> folder declares, which are the ones a generator cannot derive.</summary>
    private IReadOnlyList<ITextImporter> DeclaredImporters { get; }

    /// <summary>
    /// The language's name: what <c>language.mps</c> calls it, or the name of the folder it lives in when the
    /// project declares no root.
    /// </summary>
    public string Name { get; }

    /// <summary>The folder it was loaded from, which is also where a save writes.</summary>
    public string Directory { get; }

    /// <summary>The version the language declares for itself, or <see langword="null"/> when it declares none.</summary>
    /// <remarks>
    /// Text as the author spelled it - <c>1.2.0</c>, <c>1.2.0-preview.3</c> - and not a number this layer parses:
    /// nothing acts on it, and a host that shows it must show the claim rather than a normalised form of it.
    /// </remarks>
    public string? Version { get; }

    /// <summary>What the language is for, in the author's words, or <see langword="null"/> when it says nothing.</summary>
    /// <remarks>
    /// The sentence beside the identity, kept as the author wrote it: a host shows it where a person decides whether
    /// this is the language they were looking for, and an export carries it into the assembly.
    /// </remarks>
    public string? Description { get; }

    /// <summary>The languages this one says it builds on, in the order its root names them.</summary>
    /// <remarks>
    /// Declared, not resolved: these are the names the root wrote. <see cref="Resolved"/> is what the host
    /// answered for them; the two are kept apart so a host can report a declaration that nothing answers.
    /// </remarks>
    public IReadOnlyList<LanguageDependency> Dependencies { get; }

    /// <summary>The languages this one says it extends, in the order its root names them.</summary>
    /// <remarks>Declared too, for the same reason, and kept apart from <see cref="Dependencies"/> because a
    /// language that extends another says something stronger than one that merely uses it.</remarks>
    public IReadOnlyList<LanguageDependency> Extends { get; }

    /// <summary>
    /// The languages the host answered for, base before dependent and each one once, or empty when the root declared
    /// none.
    /// </summary>
    /// <remarks>
    /// These are what <see cref="Concepts"/> was built over, transitively. They are not absorbed: each stays the
    /// project it was, with its own declarations, notation and behaviour, which is the composition
    /// [ADR 0026](../../docs/adr/0026-a-dependency-resolves-to-the-vocabulary.md) decided on.
    /// </remarks>
    public IReadOnlyList<LanguageProject> Resolved { get; }

    /// <summary>
    /// The languages whose behaviour this one inherits, nearest first - the <c>extends</c> chain of its root, or
    /// empty when it has none.
    /// </summary>
    /// <remarks>
    /// A subset of <see cref="Resolved"/>: a dependency lends a vocabulary, an extension adopts the language
    /// ([ADR 0027](../../docs/adr/0027-behaviour-is-inherited-through-extends.md)). The order is the precedence the
    /// merged aspects follow - this language's own statement first, then the nearest extension's.
    /// </remarks>
    public IReadOnlyList<LanguageProject> Inherited { get; }

    /// <summary>What the structure model declared, in the order it declared it.</summary>
    public IReadOnlyList<ConceptDeclaration> Declarations { get; }

    /// <summary>
    /// The registry the declarations build: this language's own, over the declarations of every language it
    /// resolved, base first.
    /// </summary>
    public ConceptRegistry Concepts { get; }

    /// <summary>The notation of those concepts; empty when the project has no <c>editor</c> folder.</summary>
    public NotationRegistry Notations { get; }

    /// <summary>The type rules of those concepts; empty when the project has no <c>types</c> folder.</summary>
    public TypeRegistry Types { get; }

    /// <summary>The text generators the language declares; empty when it has no <c>generator</c> folder.</summary>
    /// <remarks>
    /// A language may declare several, and the host picks one by name - which is why this is a list and the framework
    /// has no "the" generator. A language without one loads, and its documents simply cannot be generated.
    /// </remarks>
    public IReadOnlyList<TextGenerator> Generators { get; }

    /// <summary>Whether the project declared a generator at all.</summary>
    public bool HasGenerators => Generators.Count > 0;

    /// <summary>The importers this project's generators can be read back with, one per generator that inverts.</summary>
    /// <remarks>
    /// A generator is already a promise about the printed form, so reading it back needs no second declaration: each
    /// importer is the inverse of one generator's templates. A generator that cannot be inverted is left out rather
    /// than failing the language - a language whose generator writes a reference or a parent's property is perfectly
    /// usable, it simply cannot be read back - and <see cref="TryCreateImporter(string, out ITextImporter?, out string?)"/>
    /// is where a host asks why.
    /// </remarks>
    public IReadOnlyList<ITextImporter> Importers
    {
        get
        {
            List<ITextImporter> importers = [.. DeclaredImporters];

            foreach (var generator in Generators)
            {
                if (TryCreate(generator, out var importer, out _))
                {
                    importers.Add(importer);
                }
            }

            return importers;
        }
    }

    /// <summary>Builds the importer that reads a generator's output back.</summary>
    /// <param name="name">The generator's name, which becomes the importer's format.</param>
    /// <param name="importer">The importer, or <see langword="null"/> when it cannot be built.</param>
    /// <param name="error">Why not, when it could not.</param>
    /// <returns>Whether the generator can be read back.</returns>
    public bool TryCreateImporter(
        string name,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ITextImporter? importer,
        out string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        // A declared importer is asked for by its own format name, and it wins over a generator of the same name: a
        // language that wrote a rule for a spelling meant that rule, not the inverse of a template.
        if (DeclaredImporters.FirstOrDefault(importer => string.Equals(importer.Format, name, StringComparison.Ordinal)) is { } declared)
        {
            importer = declared;
            error = null;

            return true;
        }

        if (Generators.FirstOrDefault(generator => string.Equals(generator.Name, name, StringComparison.Ordinal)) is not { } found)
        {
            importer = null;
            error = $"The language declares no generator '{name}'.";

            return false;
        }

        return TryCreate(found, out importer, out error);
    }

    private bool TryCreate(
        TextGenerator generator,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ITextImporter? importer,
        out string? error)
    {
        try
        {
            importer = new TemplateImporter(generator.Name, generator, Concepts, RootConcepts(generator));

            error = null;

            return true;
        }
        catch (ConceptDefinitionException exception)
        {
            importer = null;
            error = exception.Message;

            return false;
        }
    }

    /// <summary>The concepts whose template may stand at the top of a document.</summary>
    /// <remarks>
    /// Concrete, with a template, not restricted to a place in a node, and <b>not reachable through a child slot</b> -
    /// a concept that some other concept declares as a child is a part of a document, not a document. Without that last
    /// condition a stage would be as good a root as a pipeline, and every stray line of text would import as one.
    /// </remarks>
    private IReadOnlyList<string> RootConcepts(TextGenerator generator) =>
        [.. Concepts.Concepts
            .Where(concept => !concept.IsAbstract)
            .Where(concept => concept.Applicability.IsAnywhere)
            .Where(concept => generator.Templates.TemplateFor(concept) is not null)
            .Where(concept => !Concepts.Concepts.Any(owner => owner.Slots.OfType<ChildSlot>().Any(slot => concept.IsKindOf(slot.ChildConceptKey))))
            .Select(concept => concept.Key)];

    /// <summary>The rules the language states about itself; empty when it has no <c>constraints</c> folder.</summary>
    /// <remarks>
    /// <para>
    /// The declared half of the constraint aspect: the same <see cref="ConstraintSet"/> a language written in C# hands
    /// to a <see cref="ConstraintEngine"/>, lowered from rules that are models themselves. A language that declares
    /// none loads, and reports nothing of its own - which is the honest state of a language being written, and the
    /// same answer a missing <c>editor</c> or <c>types</c> folder gives.
    /// </para>
    /// <para>
    /// The framework's own rules - the ambiguity constraint, in particular - are the host's to add, not this
    /// project's: they are facts about the framework, not about this language.
    /// </para>
    /// </remarks>
    public ConstraintSet Constraints { get; }

    /// <summary>Whether the project declared a rule of its own.</summary>
    public bool HasConstraints => Constraints.Count > 0;

    /// <summary>The scope providers the language declares, or the empty registry when it has no <c>scopes</c> folder.</summary>
    /// <remarks>
    /// The declared half of the scope aspect: the same <see cref="ScopeProviderRegistry"/> a language written in C#
    /// installs on a session, lowered from rules that are models themselves. A language that declares none loads and
    /// its documents get <see cref="DefaultScopeProvider"/> - the honest state of a language being written, like a
    /// missing <c>editor</c> folder.
    /// </remarks>
    public ScopeProviderRegistry Scopes { get; }

    /// <summary>Whether the project declared a scope rule of its own.</summary>
    public bool HasScopes => Scopes.Count > 0;

    /// <summary>The typing rules the language declares, or the empty registry when it has no <c>typing</c> folder.</summary>
    /// <remarks>
    /// The declared half of the typing aspect: the same <see cref="ITypingRules"/> a language written in C# hands a
    /// session, lowered from rules that are models themselves. A language that declares none loads and its documents
    /// claim nothing typed into an empty slot, which is the honest state of a language being written - the editor's
    /// other way in, the completion list, still works.
    /// </remarks>
    public ITypingRules Typing { get; }

    /// <summary>Whether the project declared a typing rule of its own.</summary>
    public bool HasTyping { get; }

    /// <summary>The models the language ships for its users, in file order; empty when it has no <c>accessory</c> folder.</summary>
    /// <remarks>
    /// They are the templates <see cref="NewProject"/> copies into every project, so this is also what a shell lists
    /// and opens: a language's library nodes are editable language content, not a registry the runtime reads.
    /// </remarks>
    public IReadOnlyList<Model> Accessory { get; }

    /// <summary>The typing rules as the documents state them, in file order.</summary>
    /// <remarks>
    /// <see cref="Typing"/> is what the editor asks; this is what a tool that has to write the rules again reads,
    /// because a <see cref="TypingRule"/> holds a delegate and a delegate cannot be read back. Both come from one
    /// lowering, so they cannot disagree.
    /// </remarks>
    public IReadOnlyList<DeclaredTypingRule> DeclaredTypingRules { get; }

    /// <summary>Whether the project ships an <c>import</c> document of its own.</summary>
    /// <remarks>
    /// Only the declared ones: an importer a generator inverts is already covered by
    /// <see cref="HasGenerators"/>, which is what an export names. This is the flag an export needs to say that a
    /// hand-written importer is not in the generated file.
    /// </remarks>
    public bool HasDeclaredImporters => DeclaredImporters.Count > 0;

    /// <summary>The intentions the language declares, lowerable into an entry list.</summary>
    /// <remarks>
    /// The declared half of the intention aspect: the same provider a hand-written package registers with the session.
    /// A language that declares none still loads, and its documents simply have no language entries.
    /// </remarks>
    public IIntentionProvider Intentions { get; }

    /// <summary>Whether the project declared an intention of its own.</summary>
    public bool HasIntentions { get; }

    /// <summary>The checks this language brings to every document written in it.</summary>
    /// <remarks>
    /// A declared language states its rules as constraints and brings none of these, which is why the list is empty
    /// for one. A language written in C# can carry the standard catalogue, and that is the difference between the two:
    /// a constraint is a rule the language spells out, while a check is one the framework derives from the
    /// declarations - the name slots, the child slots, the property types - which is what makes it a library rather
    /// than a policy.
    /// </remarks>
    public CheckSet? Checks { get; }

    /// <summary>What the language states each of its concepts evaluates to, or <see langword="null"/> for none.</summary>
    /// <remarks>
    /// The one aspect with no declared form: the others are languages a project writes in a folder, while an evaluation
    /// rule is a computation and this repository has no model for one. So a shipped language hands its registry over and a
    /// declared one has none, which is why the page says so rather than guessing from the concepts.
    /// </remarks>
    public EvaluationRegistry? Evaluation { get; }
    /// <summary>Whether the project declared any type rule at all.</summary>
    /// <remarks>
    /// A language without one loads: every node is <see cref="UnknownType"/>, which is the honest answer
    /// when nobody said what a node evaluates to. Refusing to load would make "write the structure first"
    /// impossible, the same reason a missing <c>editor</c> folder is allowed.
    /// </remarks>
    public bool HasTypes => Types.Count > 0;

    /// <summary>Whether the project declared a notation at all.</summary>
    /// <remarks>
    /// A language without one loads: every node draws as a missing-notation cell, which is a state a language being
    /// written passes through. Refusing to load would make "write the structure first" impossible.
    /// </remarks>
    public bool HasNotation => Notations.Notations.Count > 0;

    /// <summary>Loads the language project in <paramref name="directory"/>.</summary>
    /// <param name="directory">The folder holding the aspect folders, one definition per document.</param>
    /// <param name="error">Why the project could not be loaded, or <see langword="null"/> on success.</param>
    /// <returns>The language, or <see langword="null"/> with a message.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is blank.</exception>
    /// <remarks>
    /// <para>
    /// Each aspect is a folder of documents, and a document holds exactly one definition: one concept, one
    /// enumeration, one notation, one rule or one named type, one generator. That is MPS' shape - one root per
    /// file - and it is what keeps a language reviewable: a diff of one concept is a diff of one file.
    /// </para>
    /// <para>
    /// Files are read in ordinal order of their names, and that order is the declaration order - which decides what
    /// an empty slot is filled with and how completion ranks. The file name is therefore part of the language, and
    /// naming the documents after what they hold is the way to keep the order readable.
    /// </para>
    /// <para>
    /// The notation is lowered against the concepts the structure documents just declared, so a notation for a
    /// concept that was never declared is refused here - the earliest point at which both halves exist.
    /// </para>
    /// <para>
    /// <c>language.mps</c> is read as well, beside the aspect folders rather than inside one. It is optional, and a
    /// project without it keeps what every project had before it existed: the folder's own name and no declared
    /// version or dependency. When it is there it is held to the same rule as any other document - one root, and a
    /// broken one is the message that names it, not a half-read language.
    /// </para>
    /// <para>
    /// A root that declares dependencies or extensions resolves them through <paramref name="resolver"/>, and the
    /// language's own registry is then built over their declarations - base first, this language last. The bases are
    /// not absorbed: their own declarations, notation and behaviour stay theirs, which is the composition
    /// [ADR 0026](../../docs/adr/0026-a-dependency-resolves-to-the-vocabulary.md) decided on. Without a resolver a
    /// declaration cannot be answered, and a definition that names a base's concept therefore still fails here - but
    /// with a message that says which language was not resolved.
    /// </para>
    /// </remarks>
    public static LanguageProject? Open(string directory, out string? error) => Open(directory, null, out error);

    /// <summary>
    /// A language that ships with a host rather than living in a folder, so a declared project can name it as a
    /// dependency.
    /// </summary>
    /// <param name="name">The name a <c>language.mps</c> writes in <c>depends on</c> or <c>extends</c>.</param>
    /// <param name="version">The version the language declares for itself, or <see langword="null"/> for none.</param>
    /// <param name="declarations">Its concepts, which is what a dependent's registry is built over.</param>
    /// <param name="concepts">Its own registry, built from <paramref name="declarations"/>.</param>
    /// <param name="notations">How it draws itself.</param>
    /// <param name="constraints">The rules it states about itself.</param>
    /// <param name="intentions">The edits it offers.</param>
    /// <param name="generators">The generators it ships.</param>
    /// <param name="types">Its type rules, or <see cref="TypeRegistry.Empty"/> for none.</param>
    /// <param name="scopes">Its scope providers, or <see cref="ScopeProviderRegistry.Empty"/> for none.</param>
    /// <param name="description">What the language is for, or <see langword="null"/> when it says nothing.</param>
    /// <returns>A project that resolves and inherits like a loaded one, with no folder behind it.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is what makes a base language a base language: MPS' <c>baseLanguage</c> is always there, and a DSL that
    /// needs an expression declares a slot over it instead of growing one of its own. A host therefore has two kinds
    /// of language to answer a declared name with - the folders the user opened, and the packages it ships - and
    /// this is the second kind expressed as the first, so <see cref="Open"/> needs no second code path.
    /// </para>
    /// <para>
    /// There is no folder, so <see cref="Directory"/> is empty and the project can never be saved back. The name is
    /// what identifies it, which is why <see cref="Name"/> comes from the root this factory writes rather than from a
    /// directory - and why the cycle check compares the name as well as the directory.
    /// </para>
    /// <para>
    /// Typing and intentions are asked for rather than derived: a shipped language has no <c>typing</c> or
    /// <c>intentions</c> folder to read, so what it offers is whatever its package built, and
    /// <see cref="HasTyping"/> and <see cref="HasIntentions"/> say whether it declared any.
    /// </para>
    /// </remarks>
    public static LanguageProject Shipped(
        string name,
        string? version,
        IReadOnlyList<ConceptDeclaration> declarations,
        ConceptRegistry concepts,
        NotationRegistry notations,
        ConstraintSet constraints,
        IIntentionProvider intentions,
        IReadOnlyList<TextGenerator> generators,
        TypeRegistry? types = null,
        ScopeProviderRegistry? scopes = null,
        ITypingRules? typing = null,
        bool hasTyping = false,
        bool hasIntentions = false,
        string? description = null,
        CheckSet? checks = null,
        EvaluationRegistry? evaluation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(concepts);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(intentions);
        ArgumentNullException.ThrowIfNull(generators);

        return new LanguageProject(
            directory: string.Empty,
            new LanguageRoot(name, version, description, [], []),
            resolved: [],
            inherited: [],
            declarations,
            concepts,
            notations,
            types ?? TypeRegistry.Empty,
            generators,
            constraints,
            scopes ?? ScopeProviderRegistry.Empty,
            typing ?? TypingRuleRegistry.Empty,
            typingRules: [],
            hasTyping,
            accessory: [],
            intentions,
            hasIntentions,
            importers: [],
            checks: checks,
            evaluation: evaluation);
    }

    /// <summary>Opens the language in <paramref name="directory"/>, resolving its declared bases through the host.</summary>
    /// <param name="directory">The language's folder.</param>
    /// <param name="resolver">What answers a declared dependency's name, or <see langword="null"/> for none.</param>
    /// <param name="error">Why the folder is not a usable language, or <see langword="null"/> on success.</param>
    /// <returns>The loaded language, or <see langword="null"/> when it could not be read.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is blank.</exception>
    public static LanguageProject? Open(string directory, ILanguageResolver? resolver, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!System.IO.Directory.Exists(Path.Combine(directory, StructureFolder)))
        {
            error = MissingStructure(directory);

            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(directory);
            var languageRoot = ReadLanguageRoot(fullPath);
            var bases = Resolve(languageRoot, resolver, fullPath);
            var definitions = Read(fullPath, StructureFolder, new Project(StructureLanguage.Concepts));
            var declarations = StructureLowering.Lower(definitions);
            var concepts = ConceptRegistry.Create(
            [
                .. bases.All.SelectMany(language => language.Declarations),
                .. declarations,
            ]);
            var scopes = LowerScopeProviders(fullPath, concepts);
            var typing = LowerTypingRules(fullPath, concepts);
            var intentions = LowerIntentions(fullPath, concepts);
            var types = LowerTypes(fullPath, concepts);
            var evaluation = LowerEvaluation(fullPath, concepts);
            var notation = LowerNotation(fullPath, concepts);

            EnsureOnlyOwnNotations(notation, declarations);

            // Behaviour is inherited through `extends` only, own statement first and the nearest base next
            // ([ADR 0027](../../docs/adr/0027-behaviour-is-inherited-through-extends.md)).
            var inheritedTypes = types.Registry;
            var inheritedConstraints = LowerConstraints(fullPath, concepts).With(types.Complaints);
            var inheritedScopes = scopes.Registry;
            List<ITypingRules> typingSets = [typing.Registry];
            List<IIntentionProvider> intentionSets = [intentions.Provider];

            foreach (var inherited in bases.Extended)
            {
                inheritedTypes = inheritedTypes.With(inherited.Types);
                inheritedConstraints = inheritedConstraints.With(inherited.Constraints);
                inheritedScopes = inheritedScopes.With(inherited.Scopes);
                typingSets.Add(inherited.Typing);
                intentionSets.Add(inherited.Intentions);
            }

            error = null;

            return new LanguageProject(
                fullPath,
                languageRoot,
                bases.All,
                bases.Extended,
                declarations,
                concepts,
                notation,
                inheritedTypes,
                LowerGenerators(fullPath, concepts),
                inheritedConstraints,
                inheritedScopes,
                ChainedTypingRules.Of(typingSets),
                typing.Rules,
                typing.HasAny || bases.Extended.Any(language => language.HasTyping),
                [
                    .. bases.All
                        .SelectMany(language => language.Accessory)
                        .Select(accessory => Rehome(accessory, concepts)),
                    .. ReadAccessory(fullPath, concepts),
                ],
                ChainedIntentions.Of(intentionSets),
                intentions.HasAny || bases.Extended.Any(language => language.HasIntentions),
                LowerImporters(fullPath, concepts),
                checks: null,
                evaluation);
        }
        catch (Exception exception) when (exception is ModelFormatException
            or ConceptDefinitionException
            or NotationException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            error = exception.Message;

            return null;
        }
    }

    /// <summary>The languages a language stands on: what it resolved, and what it inherits behaviour from.</summary>
    /// <param name="All">Every resolved language, base before dependent.</param>
    /// <param name="Extended">Those reached along an <c>extends</c> edge, nearest first.</param>
    /// <remarks>
    /// The two sets differ because the root's two lists do: a dependency lends a vocabulary, an extension is an
    /// adoption of the language's behaviour as well ([ADR 0027](../../docs/adr/0027-behaviour-is-inherited-through-extends.md)).
    /// </remarks>
    private sealed record Bases(List<LanguageProject> All, List<LanguageProject> Extended);

    /// <summary>The languages the root's two lists name, base before dependent and each one once.</summary>
    /// <param name="root">The language root, or <see langword="null"/> when the folder declares none.</param>
    /// <param name="resolver">What answers a name.</param>
    /// <param name="directory">The folder that declares them, which is where a cycle would close.</param>
    /// <exception cref="ConceptDefinitionException">
    /// A declared name has no answer, or the declarations form a cycle.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Post-order over the declared names, so a base is collected before the language that extends it and a diamond
    /// is collected once. The visited list is identity by folder, not by name: two folders may declare one name, and
    /// what must not be counted twice is the folder.
    /// </para>
    /// <para>
    /// The second set is walked again, along <c>extends</c> only and nearest first, because that is the order
    /// behaviour is inherited in: the language's own statement wins, then its nearest base's, then that one's.
    /// </para>
    /// </remarks>
    private static Bases Resolve(
        LanguageRoot? root,
        ILanguageResolver? resolver,
        string directory)
    {
        var all = new List<LanguageProject>();

        if (root is null)
        {
            return new Bases(all, []);
        }

        var path = new List<LanguageProject>();

        foreach (var declared in root.Dependencies.Concat(root.Extends))
        {
            Collect(declared);
        }

        var extended = new List<LanguageProject>();

        foreach (var declared in root.Extends)
        {
            Mark(declared);
        }

        return new Bases(all, extended);

        LanguageProject Answer(LanguageDependency declared) =>
            resolver?.Find(declared.Name)
                ?? throw new ConceptDefinitionException(
                    $"'{Path.GetFileName(directory)}' declares a dependency on the language '{declared.Name}', which the host did not resolve. Open that language first, or give the host a resolver that answers it.");

        void Collect(LanguageDependency declared)
        {
            var project = Answer(declared);

            if (string.Equals(project.Directory, directory, StringComparison.OrdinalIgnoreCase)
                || path.Any(language => Same(language, project)))
            {
                throw Cycle([.. path.Select(language => language.Name), project.Name], declared.Name);
            }

            if (all.Any(language => Same(language, project)))
            {
                return;
            }

            path.Add(project);

            foreach (var nested in project.Dependencies.Concat(project.Extends))
            {
                Collect(nested);
            }

            path.RemoveAt(path.Count - 1);
            all.Add(project);
        }

        void Mark(LanguageDependency declared)
        {
            var project = Answer(declared);

            if (extended.Any(language => Same(language, project)))
            {
                return;
            }

            extended.Add(project);

            foreach (var nested in project.Extends)
            {
                Mark(nested);
            }
        }

        static bool Same(LanguageProject left, LanguageProject right) =>
            string.Equals(left.Directory, right.Directory, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Name, right.Name, StringComparison.Ordinal);

        ConceptDefinitionException Cycle(IReadOnlyList<string> chain, string declared) =>
            new($"The language '{Path.GetFileName(directory)}' cannot resolve '{declared}': {string.Join(" -> ", chain)} is a cycle, and a language cannot be built on itself.");
    }

    /// <summary>A base's accessory model, rebuilt in this language's registry.</summary>
    /// <param name="accessory">The model, written in the base language's concepts.</param>
    /// <param name="concepts">The registry the copy has to belong to.</param>
    /// <remarks>
    /// A node belongs to one concept instance: <see cref="Model.CopyTo"/> refuses a concept that is not the target
    /// project's own, and the merged registry created new instances for the same keys. So the model is written and
    /// read back, which maps every concept by key and keeps every node id - which is exactly what a reference to a
    /// library node stores, so a document that points at one keeps pointing at it.
    /// </remarks>
    private static Model Rehome(Model accessory, ConceptRegistry concepts) =>
        ModelXml.Deserialize(new Project(concepts), ModelXml.Serialize(accessory));

    /// <summary>Refuses a notation this language wrote for a concept it does not declare itself.</summary>
    /// <remarks>
    /// Resolution makes a base's concepts visible to this project's lowering, which would let it draw one of them -
    /// an override. Two languages drawing one concept in one variant cannot coexist in a composed registry, so the
    /// conflict is reported where it was written instead of when the host composes the two.
    /// </remarks>
    private static void EnsureOnlyOwnNotations(NotationRegistry notation, IReadOnlyList<ConceptDeclaration> declarations)
    {
        var own = declarations.Select(declaration => declaration.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var written in notation.Notations)
        {
            if (!own.Contains(written.ConceptKey))
            {
                throw new NotationException(
                    $"This language gives concept '{written.ConceptKey}' a notation, but does not declare it; a language draws its own concepts, and a base's notation stays with its base.");
            }
        }
    }

    /// <summary>The one root of every document in one aspect folder, in ordinal order of their file names.</summary>
    /// <remarks>
    /// A document that holds no root or more than one is refused rather than searched: a document with two roots is
    /// two definitions in one file, which is the shape this layout exists to remove, and a document with none is a
    /// file the user will otherwise wonder about.
    /// </remarks>
    private static List<Node> Read(string languageDirectory, string aspect, Project project)
    {
        var folder = Path.Combine(languageDirectory, aspect);

        if (!System.IO.Directory.Exists(folder))
        {
            return [];
        }

        var files = System.IO.Directory
            .EnumerateFiles(folder, "*.mps", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();

        var roots = new List<Node>(files.Count);

        foreach (var file in files)
        {
            var model = ModelFile.Load(project, file);

            roots.Add(model.Roots.Count switch
            {
                1 => model.Roots[0],
                0 => throw new ConceptDefinitionException(
                    $"'{Path.GetFileName(file)}' holds no definition; a document in '{aspect}' holds exactly one."),
                _ => throw new ConceptDefinitionException(
                    $"'{Path.GetFileName(file)}' holds {model.Roots.Count} definitions; a document in '{aspect}' holds exactly one, so each definition gets its own file."),
            });
        }

        return roots;
    }

    /// <summary>The language root, or <see langword="null"/> when the project declares none.</summary>
    /// <param name="languageDirectory">The language's folder, where <c>language.mps</c> sits.</param>
    /// <exception cref="ConceptDefinitionException">
    /// The file holds no root or more than one, or what it holds is not a readable language root.
    /// </exception>
    /// <remarks>
    /// One root per document holds here too, and for the same reason: two roots in <c>language.mps</c> is two
    /// languages in one file, and taking the first would hide the second. A missing file is not a failure - the
    /// folder name is the fallback name every project had before this document existed.
    /// </remarks>
    private static LanguageRoot? ReadLanguageRoot(string languageDirectory)
    {
        var path = Path.Combine(languageDirectory, LanguageFile);

        if (!File.Exists(path))
        {
            return null;
        }

        var model = ModelFile.Load(new Project(StructureLanguage.Concepts), path);

        return model.Roots.Count switch
        {
            1 => StructureLowering.ReadLanguage(model.Roots[0]),
            0 => throw new ConceptDefinitionException(
                $"'{LanguageFile}' holds no language root; it says what the language is called and holds exactly one root."),
            _ => throw new ConceptDefinitionException(
                $"'{LanguageFile}' holds {model.Roots.Count} roots; the language root is one document with one root."),
        };
    }

    /// <summary>The models in <c>accessory</c>, loaded once and copied into every project afterwards.</summary>
    /// <param name="directory">The language's folder.</param>
    /// <param name="concepts">The concepts those models are written in, which the structure documents just declared.</param>
    /// <remarks>
    /// Whole models rather than one root each, unlike every aspect reader: an accessory is an ordinary document of the
    /// language, so it may hold as many roots as its author wants - a palette is a list, not a definition. Loading
    /// happens here, at open time, so a broken accessory is a load message naming its file rather than an exception the
    /// first time somebody creates a document.
    /// </remarks>
    private static IReadOnlyList<Model> ReadAccessory(string directory, ConceptRegistry concepts)
    {
        var folder = Path.Combine(directory, AccessoryFolder);

        if (!System.IO.Directory.Exists(folder))
        {
            return [];
        }

        return
        [
            .. System.IO.Directory
                .EnumerateFiles(folder, "*.mps", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Select(path => ModelFile.Load(new Project(concepts), path)),
        ];
    }

    /// <summary>Why a folder with no <c>structure</c> directory is not a language project.</summary>    /// <remarks>
    /// The old layout - one <c>structure.mps</c> holding the whole language - is named explicitly, because the file
    /// that used to be the answer is still sitting there and "not a language project" would send its author looking
    /// for a different problem.
    /// </remarks>
    private static string MissingStructure(string languageDirectory)
    {
        var legacy = Path.Combine(languageDirectory, StructureFolder + ".mps");

        return File.Exists(legacy)
            ? $"'{languageDirectory}' holds no {StructureFolder} folder but a {StructureFolder}.mps: a language keeps one definition per document now, so split that file into {StructureFolder}\\*.mps, one concept or enumeration each."
            : $"'{languageDirectory}' holds no {StructureFolder} folder, so it is not a language project.";
    }

    /// <summary>A project in which a document of this language can be created or loaded.</summary>
    /// <remarks>
    /// The accessory models come with it, copied in with their own identities: a document that points a reference at
    /// a palette entry, a standard unit or a ready-made type stores that node's id, so the id has to be the same in
    /// every project the document is ever loaded into - which is what <see cref="Model.CopyTo"/> preserves and
    /// <see cref="Model.InsertCopy"/> deliberately does not.
    /// </remarks>
    public Project NewProject()
    {
        var project = new Project(Concepts);

        foreach (var accessory in Accessory)
        {
            accessory.CopyTo(project);
        }

        return project;
    }

    /// <summary>A model of this language, ready for one document.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public Model NewModel(string name = "model") => NewProject().NewModel(name);

    /// <summary>The two products of a language's types aspect: what its concepts evaluate to, and what they report.</summary>
    private readonly record struct TypeAspect(TypeRegistry Registry, ConstraintSet Complaints);

    /// <remarks>
    /// The documents are read once and lowered twice, because a types document states two separate things: a
    /// type per concept, and a complaint per concept that reports instead of typing. Reading twice would parse
    /// the same models twice for no gain.
    /// </remarks>
    /// <summary>The rules the <c>evaluation</c> folder declares, or <see langword="null"/> when it holds none.</summary>
    /// <remarks>
    /// Null rather than an empty registry, because "this language states no meaning" is a fact the documentation page and a
    /// host both report differently from "it states these three" - and an empty registry would say the second.
    /// </remarks>
    private static EvaluationRegistry? LowerEvaluation(string directory, ConceptRegistry concepts)
    {
        var documents = Read(directory, EvaluationFolder, new Project(EvaluationRuleLanguage.Concepts));

        return documents.Count == 0 ? null : EvaluationRuleLowering.CreateRegistry(documents, concepts);
    }

    private static TypeAspect LowerTypes(string directory, ConceptRegistry concepts)
    {
        var documents = Read(directory, TypesFolder, new Project(TypeRuleLanguage.Concepts));

        return new TypeAspect(
            TypeRuleLowering.CreateRegistry(documents, concepts),
            TypeRuleLowering.CreateComplaints(documents));
    }

    private static NotationRegistry LowerNotation(string directory, ConceptRegistry concepts)
    {
        var notations = Read(directory, EditorFolder, new Project(EditorLanguage.Concepts));

        return notations.Count > 0
            ? EditorLowering.CreateRegistry(notations, concepts)
            : new NotationRegistry(concepts);
    }

    /// <summary>Every generator the project declares, or none when it has no <c>generator</c> folder.</summary>
    /// <remarks>
    /// The lowering is given the concepts the structure documents just declared, so a mapping that names a concept
    /// this language does not have is refused here - the same "earliest moment both halves exist" rule the notation
    /// follows.
    /// </remarks>
    /// <summary>The importers the project declares in its <c>import</c> folder, or none when it has no such folder.</summary>
    private static IReadOnlyList<ITextImporter> LowerImporters(string directory, ConceptRegistry concepts) =>
        ImportLowering.CreateImporters(
            Read(directory, ImportFolder, new Project(ImportLanguage.Concepts)),
            concepts);

    private static IReadOnlyList<TextGenerator> LowerGenerators(string directory, ConceptRegistry concepts) =>
        TextGenLowering.CreateGenerators(
            Read(directory, GeneratorFolder, new Project(TextGenLanguage.Concepts)),
            concepts);

    /// <summary>Every rule the project declares, or none when it has no <c>constraints</c> folder.</summary>
    /// <remarks>
    /// The lowering is given the concepts the structure documents just declared, so a rule that names a concept or a
    /// slot this language does not have is refused here - the same "earliest moment both halves exist" rule the
    /// notation and the templates follow.
    /// </remarks>
    private static ConstraintSet LowerConstraints(string directory, ConceptRegistry concepts) =>
        ConstraintLowering.CreateConstraints(
            Read(directory, ConstraintsFolder, new Project(ConstraintLanguage.Concepts)),
            concepts);

    /// <summary>Every scope rule the project declares, or none when it has no <c>scopes</c> folder.</summary>
    /// <remarks>
    /// The lowering is given the concepts the structure documents just declared, so a rule naming a concept or a
    /// reference slot this language does not have is refused here - the same "earliest moment both halves exist"
    /// rule the notation, the templates and the constraints follow.
    /// </remarks>
    private static (ScopeProviderRegistry Registry, bool HasAny) LowerScopeProviders(string directory, ConceptRegistry concepts)
    {
        var documents = Read(directory, ScopesFolder, new Project(ScopeLanguage.Concepts));

        return (ScopeLowering.CreateProviders(documents, concepts), documents.Count > 0);
    }

    private static (IIntentionProvider Provider, bool HasAny) LowerIntentions(string directory, ConceptRegistry concepts)
    {
        var documents = Read(directory, IntentionsFolder, new Project(IntentionLanguage.Concepts));

        return (IntentionLowering.CreateProvider(documents, concepts), documents.Count > 0);
    }

    /// <summary>Every typing rule the project declares, or none when it has no <c>typing</c> folder.</summary>
    /// <remarks>
    /// The lowering is given the concepts the structure documents just declared, so a rule that names a concept this
    /// language does not have, or a property it does not declare, is refused here - the same "earliest moment both
    /// halves exist" rule the notation, the templates, the constraints and the scopes follow. The words are kept
    /// beside the registry because a predicate cannot be read back and the C# export has to write the rules again.
    /// </remarks>
    private static (ITypingRules Registry, IReadOnlyList<DeclaredTypingRule> Rules, bool HasAny) LowerTypingRules(
        string directory,
        ConceptRegistry concepts)
    {
        var documents = Read(directory, TypingFolder, new Project(TypingLanguage.Concepts));
        var (registry, rules) = TypingLowering.Lower(documents, concepts);

        return (registry, rules, documents.Count > 0);
    }
}
