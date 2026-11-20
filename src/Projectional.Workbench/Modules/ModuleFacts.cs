using System.Globalization;

using Projectional.Languages;
using Projectional.Testing;

namespace Projectional.Workbench;

/// <summary>What a language module is made of, read from its folder and from what the shell loaded out of it.</summary>
/// <remarks>
/// <para>
/// A module's settings dialog answers three questions - where does this module live, what is in it, and what did the
/// shell make of it - and the third is the one nobody can see from the outside: a language whose constraints failed to
/// load looks exactly like one that declares none until a document misbehaves. Deriving all of it here, in a type with
/// no Avalonia in it, keeps the answers testable and leaves the dialog as layout.
/// </para>
/// <para>
/// Every aspect is listed, including the ones the module does not have. The explorer hides an empty folder because a
/// tree is about what is there; settings are about what could be there, and "no constraints" is a fact the author wants
/// to read rather than to infer from a row that is missing.
/// </para>
/// <para>
/// The roots are folder names rather than paths: they all sit inside the module, whose own path the dialog shows once,
/// and a list of six absolute paths is a list nobody reads to the end.
/// </para>
/// </remarks>
internal sealed record ModuleFacts(
    string Name,
    string Directory,
    IReadOnlyList<ModuleAspect> Aspects,
    IReadOnlyList<string> Roots,
    IReadOnlyList<ModuleFact> Contents)
{
    /// <summary>The language each aspect folder's documents are written in, in the order the aspects are listed.</summary>
    /// <remarks>
    /// The definition languages the workbench ships, one per aspect. Spelled here rather than read from a package
    /// because an aspect is a folder and the language that reads it is the shell's decision - the same table the
    /// shell's editors are registered from.
    /// </remarks>
    private static readonly (string Folder, string Language)[] AspectLanguages =
    [
        (LanguageProject.StructureFolder, "Structure"),
        (LanguageProject.EditorFolder, "Editor"),
        (LanguageProject.TypesFolder, "Types"),
        (LanguageProject.ConstraintsFolder, "Constraints"),
        (LanguageProject.ScopesFolder, "Scopes"),
        (LanguageProject.IntentionsFolder, "Intentions"),
        (LanguageProject.ImportFolder, "Import"),
        (LanguageProject.GeneratorFolder, "Generator"),
        (TestRunner.Folder, "Tests"),
    ];

    /// <summary>Reads <paramref name="module"/>, its folder and what the shell lowered out of it.</summary>
    /// <param name="module">The module to describe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> is <see langword="null"/>.</exception>
    internal static ModuleFacts Of(LanguageProject module)
    {
        ArgumentNullException.ThrowIfNull(module);

        var aspects = new List<ModuleAspect>();

        foreach (var (folder, language) in AspectLanguages)
        {
            aspects.Add(new ModuleAspect(folder, language, Documents(System.IO.Path.Combine(module.Directory, folder))));
        }

        return new ModuleFacts(
            module.Name,
            module.Directory,
            aspects,
            [.. aspects.Where(aspect => aspect.IsPresent).Select(aspect => aspect.Name)],
            Summary(module));
    }

    /// <summary>How many definition documents <paramref name="folder"/> holds, and zero when it does not exist.</summary>
    private static int Documents(string folder)
    {
        try
        {
            return System.IO.Directory.Exists(folder)
                ? System.IO.Directory.EnumerateFiles(folder, "*.mps", SearchOption.TopDirectoryOnly).Count()
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>What the shell loaded out of the module, as the rows the contents page shows.</summary>
    /// <remarks>
    /// Counts of what a document can use rather than counts of documents: a language with two hundred declarations and
    /// no notation cannot be drawn, and seeing <c>Notations 0</c> is how an author finds that out without opening a
    /// document and wondering why the page is empty.
    /// </remarks>
    private static IReadOnlyList<ModuleFact> Summary(LanguageProject module) =>
    [
        new("Declared concepts", Number(module.Declarations.Count)),
        new("Notations", Number(module.Notations.Notations.Count)),
        new("Type rules", Number(module.Types.Count)),
        new("Constraints", Number(module.Constraints.Count)),
        new("Scope rules", Number(module.Scopes.Count)),
        new("Intentions", module.HasIntentions ? "declared" : "none"),
        new("Generators", Number(module.Generators.Count)),
        new("Importers", Number(module.Importers.Count)),
    ];

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
