using Projectional.Core;
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
    /// <summary>The folder holding the concepts and enumerations, which every language project needs.</summary>
    public const string StructureFolder = "structure";

    /// <summary>The folder holding the notation, which a language may leave out while it is being written.</summary>
    public const string EditorFolder = "editor";

    /// <summary>The folder holding the type rules and the named types, which a language may leave out.</summary>
    public const string TypesFolder = "types";

    /// <summary>The folder holding the text generators, which a language may leave out until it has any.</summary>
    public const string GeneratorFolder = "generator";

    /// <summary>The folder holding the language's own constraints, which a language may leave out.</summary>
    public const string ConstraintsFolder = "constraints";

    /// <summary>The folder holding the language's own scope rules, which a language may leave out.</summary>
    public const string ScopesFolder = "scopes";

    /// <summary>The folder holding the language's own typing rules, which a language may leave out.</summary>
    public const string TypingFolder = "typing";

    /// <summary>The folder holding the language's declared importers, which a language may leave out.</summary>
    public const string ImportFolder = "import";

    /// <summary>The folder holding the language's intentions, which a language may leave out.</summary>
    public const string IntentionsFolder = "intentions";

    private LanguageProject(
        string name,
        string directory,
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
        IIntentionProvider intentions,
        bool hasIntentions,
        IReadOnlyList<ITextImporter> importers)
    {
        Name = name;
        Directory = directory;
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
        Intentions = intentions;
        HasIntentions = hasIntentions;
        DeclaredImporters = importers;
    }

    /// <summary>The importers the <c>import</c> folder declares, which are the ones a generator cannot derive.</summary>
    private IReadOnlyList<ITextImporter> DeclaredImporters { get; }

    /// <summary>The language's name, which is the name of the folder it lives in.</summary>
    public string Name { get; }

    /// <summary>The folder it was loaded from, which is also where a save writes.</summary>
    public string Directory { get; }

    /// <summary>What the structure model declared, in the order it declared it.</summary>
    public IReadOnlyList<ConceptDeclaration> Declarations { get; }

    /// <summary>The registry those declarations build.</summary>
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
    /// </remarks>
    public static LanguageProject? Open(string directory, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!System.IO.Directory.Exists(Path.Combine(directory, StructureFolder)))
        {
            error = MissingStructure(directory);

            return null;
        }

        try
        {
            var definitions = Read(directory, StructureFolder, new Project(StructureLanguage.Concepts));
            var declarations = StructureLowering.Lower(definitions);
            var concepts = ConceptRegistry.Create(declarations);
            var scopes = LowerScopeProviders(directory, concepts);
            var typing = LowerTypingRules(directory, concepts);
            var intentions = LowerIntentions(directory, concepts);
            var types = LowerTypes(directory, concepts);

            error = null;

            return new LanguageProject(
                new DirectoryInfo(Path.GetFullPath(directory)).Name,
                Path.GetFullPath(directory),
                declarations,
                concepts,
                LowerNotation(directory, concepts),
                types.Registry,
                LowerGenerators(directory, concepts),
                LowerConstraints(directory, concepts).With(types.Complaints),
                scopes.Registry,
                typing.Registry,
                typing.Rules,
                typing.HasAny,
                intentions.Provider,
                intentions.HasAny,
                LowerImporters(directory, concepts));
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

    /// <summary>Why a folder with no <c>structure</c> directory is not a language project.</summary>
    /// <remarks>
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
    public Project NewProject() => new(Concepts);

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
