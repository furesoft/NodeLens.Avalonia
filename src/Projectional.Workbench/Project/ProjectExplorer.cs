using Projectional.Languages;
using Projectional.Testing;

namespace Projectional.Workbench;

/// <summary>
/// The rows of the project explorer: what is open, and what the opened languages are made of.
/// </summary>
/// <remarks>
/// <para>
/// The outline answers "what is this document made of"; this answers the question one level above it - which
/// documents are open, and which files the languages opened from a folder ship - so the two sit beside each
/// other instead of walking the same model twice.
/// </para>
/// <para>
/// Only a language's folders are read from disk, and they are read every time the rows are built: a file added
/// or deleted shows up the next time the panel is filled, which is the same rule the outline follows for the
/// model. Open documents are the tab strip's own list, so the two cannot disagree about what is open.
/// </para>
/// <para>
/// <paramref name="collapsed"/> is the view state the rows are derived from - never mutated here. A node in it is
/// drawn shut and its children are not built at all, which is why a closed project is one row and not a tree that is
/// merely hidden.
/// </para>
/// </remarks>
internal static class ProjectExplorer
{
    /// <summary>The folders a language project is made of, in the order the editor documents them.</summary>
    /// <remarks>
    /// The order is the reading order of a language: what it declares, how it is drawn, and then the aspects that
    /// give it meaning, ending with the tests that prove it.
    /// </remarks>
    private static readonly string[] Folders =
    [
        LanguageProject.StructureFolder,
        LanguageProject.EditorFolder,
        LanguageProject.TypesFolder,
        LanguageProject.ConstraintsFolder,
        LanguageProject.ScopesFolder,
        LanguageProject.IntentionsFolder,
        LanguageProject.ImportFolder,
        LanguageProject.GeneratorFolder,
        TestRunner.Folder,
    ];


    /// <summary>The key of the languages heading.</summary>
    internal const string LanguagesKey = "languages";

    /// <summary>The key of one project's row.</summary>
    /// <param name="language">The project.</param>
    internal static string ProjectKey(LanguageProject language) => "project:" + language.Directory;

    /// <summary>The key of one folder's row.</summary>
    /// <param name="directory">The project's folder.</param>
    /// <param name="folder">The aspect folder.</param>
    internal static string FolderKey(string directory, string folder) => "folder:" + Path.Combine(directory, folder);

    /// <summary>Every key a project contributes: its own row and one per folder it actually has.</summary>
    /// <param name="language">The project.</param>
    /// <remarks>
    /// A caller that wants the whole tree shut asks this rather than guessing folder names: which folders exist is a
    /// fact about the folder on disk, and the same walk that builds the rows answers it.
    /// </remarks>
    internal static IReadOnlyList<string> KeysOf(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return [ProjectKey(language), .. FolderKeysOf(language)];
    }

    /// <summary>The key of each aspect folder a project actually has.</summary>
    /// <param name="language">The project.</param>
    /// <remarks>
    /// Apart from <see cref="KeysOf"/> because the two questions differ: closing a project hides its folders, while
    /// closing the folders leaves the projects listed as what they consist of.
    /// </remarks>
    internal static IReadOnlyList<string> FolderKeysOf(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return
        [
            .. FoldersOf(language.Directory).Select(folder => FolderKey(language.Directory, folder.Folder)),
        ];
    }

    /// <summary>The rows, in the order the explorer draws them.</summary>
    /// <param name="documents">The open documents, which is only how a file row learns it is open - the explorer lists the project, not the tabs.</param>
    /// <param name="languages">The language projects this window has opened, in the order they joined.</param>
    /// <param name="collapsed">The keys of the nodes drawn shut.</param>
    internal static IReadOnlyList<ProjectEntry> Of(
        IReadOnlyList<DocumentTab> documents,
        IReadOnlyList<LanguageProject> languages,
        IReadOnlySet<string> collapsed)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(collapsed);

        var rows = new List<ProjectEntry>();

        if (languages.Count == 0)
        {
            return rows;
        }

        var languagesOpen = !collapsed.Contains(LanguagesKey);

        rows.Add(new ProjectEntry("Languages", 0, ProjectNode.Group, LanguagesKey, languagesOpen));

        if (!languagesOpen)
        {
            return rows;
        }

        foreach (var language in languages)
        {
            var projectKey = ProjectKey(language);
            var projectOpen = !collapsed.Contains(projectKey);

            rows.Add(new ProjectEntry(language.Name, 1, ProjectNode.Project, projectKey, projectOpen, module: language));

            if (!projectOpen)
            {
                continue;
            }

            foreach (var (folder, files) in FoldersOf(language.Directory))
            {
                var folderKey = FolderKey(language.Directory, folder);
                var folderOpen = !collapsed.Contains(folderKey);

                rows.Add(new ProjectEntry(folder, 2, ProjectNode.Folder, folderKey, folderOpen, folder: folder, module: language));

                if (!folderOpen)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    rows.Add(new ProjectEntry(
                        Path.GetFileName(file),
                        3,
                        ProjectNode.File,
                        path: file,
                        document: OpenDocumentFor(documents, file),
                        folder: folder,
                        module: language));
                }
            }
        }

        return rows;
    }

    /// <summary>Every folder of a language project that holds definition documents, with its files.</summary>
    /// <param name="directory">The project's folder.</param>
    /// <remarks>
    /// A folder is a row of its own, named as it is on disk, because that is how a language's aspects are organised
    /// and what a user sees in a file manager. A missing or empty folder is not a row - a language that declares no
    /// constraints has no <c>constraints</c> folder, and an empty heading would describe nothing.
    /// </remarks>
    private static IEnumerable<(string Folder, IReadOnlyList<string> Files)> FoldersOf(string directory)
    {
        foreach (var folder in Folders)
        {
            var path = Path.Combine(directory, folder);

            if (!Directory.Exists(path))
            {
                continue;
            }

            var files = Directory
                .EnumerateFiles(path, "*.mps", SearchOption.TopDirectoryOnly)
                .OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal)
                .ToArray();

            if (files.Length > 0)
            {
                yield return (folder, files);
            }
        }
    }

    /// <summary>The open document that came from <paramref name="file"/>, or <see langword="null"/> when none did.</summary>
    private static DocumentTab? OpenDocumentFor(IReadOnlyList<DocumentTab> documents, string file)
    {
        var full = Path.GetFullPath(file);

        foreach (var document in documents)
        {
            if (document.FilePath is { } candidate
                && string.Equals(Path.GetFullPath(candidate), full, StringComparison.OrdinalIgnoreCase))
            {
                return document;
            }
        }

        return null;
    }
}