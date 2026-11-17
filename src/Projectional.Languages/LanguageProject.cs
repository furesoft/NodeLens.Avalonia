using Projectional.Core;
using Projectional.Editors;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Structures;
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
    /// <summary>The file holding the concepts, which every language project needs.</summary>
    public const string StructureFile = "structure.mps";

    /// <summary>The file holding the notation, which a language may leave out while it is being written.</summary>
    public const string EditorFile = "editor.mps";

    /// <summary>The file holding the type rules, which a language may leave out until it has any.</summary>
    public const string TypesFile = "types.mps";

    private LanguageProject(
        string name,
        string directory,
        IReadOnlyList<ConceptDeclaration> declarations,
        ConceptRegistry concepts,
        NotationRegistry notations,
        TypeRegistry types)
    {
        Name = name;
        Directory = directory;
        Declarations = declarations;
        Concepts = concepts;
        Notations = notations;
        Types = types;
    }

    /// <summary>The language's name, which is the name of the folder it lives in.</summary>
    public string Name { get; }

    /// <summary>The folder it was loaded from, which is also where a save writes.</summary>
    public string Directory { get; }

    /// <summary>What the structure model declared, in the order it declared it.</summary>
    public IReadOnlyList<ConceptDeclaration> Declarations { get; }

    /// <summary>The registry those declarations build.</summary>
    public ConceptRegistry Concepts { get; }

    /// <summary>The notation of those concepts; empty when the project has no <c>editor.mps</c>.</summary>
    public NotationRegistry Notations { get; }

    /// <summary>The type rules of those concepts; empty when the project has no <c>types.mps</c>.</summary>
    public TypeRegistry Types { get; }

    /// <summary>Whether the project declared any type rule at all.</summary>
    /// <remarks>
    /// A language without one loads: every node is <see cref="UnknownType"/>, which is the honest answer
    /// when nobody said what a node evaluates to. Refusing to load would make "write the structure first"
    /// impossible, the same reason a missing <c>editor.mps</c> is allowed.
    /// </remarks>
    public bool HasTypes => Types.Count > 0;

    /// <summary>Whether the project declared a notation at all.</summary>
    /// <remarks>
    /// A language without one loads: every node draws as a missing-notation cell, which is a state a language being
    /// written passes through. Refusing to load would make "write the structure first" impossible.
    /// </remarks>
    public bool HasNotation => Notations.Notations.Count > 0;

    /// <summary>Loads the language project in <paramref name="directory"/>.</summary>
    /// <param name="directory">The folder holding <c>structure.mps</c>, and optionally <c>editor.mps</c>.</param>
    /// <param name="error">Why the project could not be loaded, or <see langword="null"/> on success.</param>
    /// <returns>The language, or <see langword="null"/> with a message.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is blank.</exception>
    /// <remarks>
    /// The notation is lowered against the concepts the structure model just declared, so a notation for a concept
    /// that was never declared is refused here - the earliest point at which both halves exist.
    /// </remarks>
    public static LanguageProject? Open(string directory, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var structurePath = Path.Combine(directory, StructureFile);

        if (!File.Exists(structurePath))
        {
            error = $"'{directory}' holds no {StructureFile}, so it is not a language project.";

            return null;
        }

        try
        {
            var structure = ModelFile.Load(StructureLanguage.NewProject(), structurePath);
            var declarations = StructureLowering.Lower(structure.Roots[0]);
            var concepts = ConceptRegistry.Create(declarations);

            error = null;

            return new LanguageProject(
                new DirectoryInfo(Path.GetFullPath(directory)).Name,
                Path.GetFullPath(directory),
                declarations,
                concepts,
                LowerNotation(directory, concepts),
                LowerTypes(directory));
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

    /// <summary>A project in which a document of this language can be created or loaded.</summary>
    public Project NewProject() => new(Concepts);

    /// <summary>A model of this language, ready for one document.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public Model NewModel(string name = "model") => NewProject().NewModel(name);

    private static TypeRegistry LowerTypes(string directory)
    {
        var path = Path.Combine(directory, TypesFile);

        return File.Exists(path)
            ? TypeRuleLowering.CreateRegistry(ModelFile.Load(TypeRuleLanguage.NewProject(), path).Roots[0])
            : TypeRegistry.Empty;
    }

    private static NotationRegistry LowerNotation(string directory, ConceptRegistry concepts)
    {
        var path = Path.Combine(directory, EditorFile);

        return File.Exists(path)
            ? EditorLowering.CreateRegistry(ModelFile.Load(EditorLanguage.NewProject(), path).Roots[0], concepts)
            : new NotationRegistry(concepts);
    }
}
