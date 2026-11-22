using Projectional.Core;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Structures;

namespace Projectional.Workbench;

/// <summary>The example language projects shipped beside the executable, and opening one with what it stands on.</summary>
/// <remarks>
/// <para>
/// The declared samples in <c>samples/</c> are what a user opens rather than something compiled into the shell, so a
/// build copies them beside the executable and the menu needs no file picker. A publish does not copy them - the
/// single-file app has to stay one file - which is why the menu hides itself when the folder is not there.
/// </para>
/// <para>
/// An example is listed by the name it gives itself, and a language whose <c>language.mps</c> says nothing is listed
/// by its folder - <c>declared-expressions</c> reads as <c>Expressions</c>. That name is also how the dependencies are
/// found: <c>declared-invoice</c> declares <c>extends Units</c>, so opening it opens the example that calls itself
/// <c>Units</c> first instead of telling the user about a dependency they have never heard of.
/// </para>
/// </remarks>
internal static class ExampleProjects
{
    /// <summary>The folder beside the executable that a build fills from <c>samples/</c>.</summary>
    internal const string Folder = "Examples";

    /// <summary>The prefix the sample folders share, which a folder-derived name does not repeat.</summary>
    private const string SamplePrefix = "declared-";

    /// <summary>The examples under <paramref name="root"/>, in ordinal order of their folders.</summary>
    /// <param name="root">The folder holding one directory per example.</param>
    /// <returns>
    /// Every directory that holds a <c>structure</c> folder - what makes a folder a language project - and nothing
    /// else, so a stray file or a folder someone put there costs no menu entry.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="root"/> is empty.</exception>
    internal static IReadOnlyList<ExampleProject> Catalogue(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateDirectories(root)
                .Where(directory => Directory.Exists(Path.Combine(directory, LanguageProject.StructureFolder)))
                .OrderBy(directory => Path.GetFileName(directory), StringComparer.Ordinal)
                .Select(Read),
        ];
    }

    /// <summary><paramref name="chosen"/> and every example it stands on, directly or through another.</summary>
    /// <param name="chosen">The example the user picked.</param>
    /// <param name="catalogue">The examples it may be built on.</param>
    /// <returns>
    /// The examples to open, dependencies first, so a caller can add them in order. A declared cycle stops at the
    /// point it closes, which leaves the open to refuse it rather than the catalogue to loop.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="chosen"/> or <paramref name="catalogue"/> is <see langword="null"/>.
    /// </exception>
    internal static IReadOnlyList<ExampleProject> Needed(ExampleProject chosen, IReadOnlyList<ExampleProject> catalogue)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(catalogue);

        var needed = new List<ExampleProject>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        Add(chosen);

        return needed;

        void Add(ExampleProject example)
        {
            if (!seen.Add(example.Name))
            {
                return;
            }

            foreach (var name in example.StandsOn)
            {
                if (catalogue.FirstOrDefault(candidate => candidate.Name == name) is { } dependency)
                {
                    Add(dependency);
                }
            }

            needed.Add(example);
        }
    }

    /// <summary>Loads the examples in <paramref name="needed"/> and adds the ones that resolve.</summary>
    /// <param name="language">The shell's set, which every project that loads is taken into.</param>
    /// <param name="needed">The folders to open, dependencies first.</param>
    /// <param name="refused">One line per example that did not load, or empty when every one did.</param>
    /// <returns>The projects that joined the set, in the order they joined.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="language"/> or <paramref name="needed"/> is <see langword="null"/>.
    /// </exception>
    internal static IReadOnlyList<LanguageProject> Load(
        WorkbenchLanguage language,
        IReadOnlyList<ExampleProject> needed,
        out IReadOnlyList<string> refused)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(needed);

        var opened = new List<LanguageProject>();
        var reasons = new List<string>();

        foreach (var example in needed)
        {
            if (LanguageProject.Open(example.Directory, language, out var error) is not { } project)
            {
                reasons.Add($"{example.Name}: {error}");

                continue;
            }

            if (language.TryAdd(project, out error))
            {
                opened.Add(project);
            }
            else
            {
                reasons.Add($"{example.Name}: {error}");
            }
        }

        refused = reasons;

        return opened;
    }

    /// <summary>One example, read from the <c>language.mps</c> it may or may not ship.</summary>
    private static ExampleProject Read(string directory)
    {
        var folder = Path.GetFileName(directory);
        var file = Path.Combine(directory, LanguageProject.LanguageFile);

        if (!File.Exists(file))
        {
            return new ExampleProject(directory, DisplayName(folder), []);
        }

        try
        {
            var model = ModelFile.Load(new Project(StructureLanguage.Concepts), file);

            if (model.Roots.Count != 1)
            {
                return new ExampleProject(directory, DisplayName(folder), []);
            }

            var root = StructureLowering.ReadLanguage(model.Roots[0]);

            return new ExampleProject(
                directory,
                root.Name,
                [.. root.Dependencies.Select(dependency => dependency.Name), .. root.Extends.Select(dependency => dependency.Name)],
                root.Description);
        }
        catch (Exception exception) when (exception is ModelFormatException or ConceptDefinitionException)
        {
            // A root the reader refuses is the open's business, not the menu's: the entry stays, and choosing it says
            // what is wrong. A menu that vanished because one example is broken would hide the others with it.
            return new ExampleProject(directory, DisplayName(folder), []);
        }
    }

    /// <summary>The name a folder gives a language that names itself nothing: <c>declared-units</c> reads <c>Units</c>.</summary>
    private static string DisplayName(string folder)
    {
        var trimmed = folder.StartsWith(SamplePrefix, StringComparison.Ordinal) ? folder[SamplePrefix.Length..] : folder;

        return trimmed.Length == 0
            ? folder
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..].Replace('-', ' ');
    }
}
