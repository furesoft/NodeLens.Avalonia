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

    /// <summary>The rows, in the order the explorer draws them.</summary>
    /// <param name="documents">The open documents, in tab order.</param>
    /// <param name="languages">The language projects this window has opened, in the order they joined.</param>
    internal static IReadOnlyList<ProjectEntry> Of(
        IReadOnlyList<DocumentTab> documents,
        IReadOnlyList<LanguageProject> languages)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(languages);

        var rows = new List<ProjectEntry>();

        if (documents.Count > 0)
        {
            rows.Add(new ProjectEntry("Documents", 0));

            foreach (var document in documents)
            {
                rows.Add(new ProjectEntry(document.Title, 1, document, document.FilePath));
            }
        }

        var headed = false;

        foreach (var language in languages)
        {
            if (!headed)
            {
                rows.Add(new ProjectEntry("Languages", 0));
                headed = true;
            }

            rows.Add(new ProjectEntry(language.Name, 1));

            foreach (var (folder, files) in FoldersOf(language.Directory))
            {
                rows.Add(new ProjectEntry(folder, 2));

                foreach (var file in files)
                {
                    rows.Add(new ProjectEntry(
                        Path.GetFileName(file),
                        3,
                        OpenDocumentFor(documents, file),
                        file));
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