using System.Text;

using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>A language's documentation as a folder of static pages, one per concept, that a reader can open offline.</summary>
/// <remarks>
/// <para>
/// The same shape as the C# export: <see cref="Generate(LanguageProject)"/> answers the pages in memory so a test can
/// assert on them, and <see cref="Write(LanguageProject, string)"/> puts exactly those bytes on disk. Writing never
/// takes a second path through the renderers, so a page that a test approved is the page a user gets.
/// </para>
/// <para>
/// What the set covers is the <em>chain</em>: the language and everything it stands on, base first, each in its own
/// folder. A reader who follows a reference into a shipped vocabulary therefore finds the page that describes it
/// instead of a dead key. What is <em>not</em> covered is the languages that depend on this one - that is the whole
/// project and unbounded; those get their own documentation, and their chain includes this language.
/// </para>
/// <para>
/// The overloads that take a list document a whole <em>site</em>: several languages in one tree, each exactly once,
/// with one root page and one search across all of them. That is what the repository's own CI publishes, and it is the
/// reason a shared base language is a folder rather than a copy inside every dependent.
/// </para>
/// </remarks>
public static class LanguageDocumentation
{
    /// <summary>Renders every page of <paramref name="language"/>'s documentation in the order they are written.</summary>
    /// <param name="language">The language to document, with its chain already resolved.</param>
    /// <returns>The pages, the root first, then the assets, then each language of the chain base first.</returns>
    public static IReadOnlyList<DocumentationPage> Generate(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return Render(new DocumentationChain(language), language);
    }

    /// <summary>Renders one documentation set covering all of <paramref name="languages"/>.</summary>
    /// <param name="languages">The languages to document, each with its chain already resolved.</param>
    /// <returns>The pages, the root first, then the assets, then each language of the set base first.</returns>
    /// <remarks>
    /// Every language appears once even when several of them stand on it, and the root page is the set's rather than any
    /// one language's - a site is read by arriving at it, not at a language.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="languages"/> is empty, or two of its languages call themselves the same thing while living in
    /// different folders.
    /// </exception>
    public static IReadOnlyList<DocumentationPage> Generate(IReadOnlyList<LanguageProject> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);

        return Render(new DocumentationChain(languages), primary: null);
    }

    /// <summary>Writes the documentation into <paramref name="directory"/>, creating it and its subfolders.</summary>
    /// <param name="language">The language to document, with its chain already resolved.</param>
    /// <param name="directory">The folder to write into; an existing one is filled, nothing is deleted.</param>
    /// <returns>How many files were written.</returns>
    /// <remarks>
    /// UTF-8 without a byte order mark and LF line endings, for the same reason the model's own XML writer is
    /// byte-stable: the output is meant to be diffed and committed. A stale page from an earlier run is left alone
    /// rather than deleted, because the folder is the user's and this function does not know what else is in it.
    /// </remarks>
    public static int Write(LanguageProject language, string directory) => Write(Generate(language), directory);

    /// <summary>Writes one documentation set covering all of <paramref name="languages"/>.</summary>
    /// <param name="languages">The languages to document, each with its chain already resolved.</param>
    /// <param name="directory">The folder to write into; an existing one is filled, nothing is deleted.</param>
    /// <returns>How many files were written.</returns>
    public static int Write(IReadOnlyList<LanguageProject> languages, string directory) =>
        Write(Generate(languages), directory);

    /// <summary>The single sentence a host shows once the folder is written.</summary>
    /// <param name="languages">The languages that were written.</param>
    /// <param name="files">How many files were written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="languages"/> is <see langword="null"/>.</exception>
    public static string Summarise(IReadOnlyList<LanguageProject> languages, int files)
    {
        ArgumentNullException.ThrowIfNull(languages);

        var names = string.Join(", ", languages.Select(language => language.Name));

        return new StringBuilder()
            .Append(files)
            .Append(files == 1 ? " page" : " pages")
            .Append(" for ")
            .Append(names.Length == 0 ? "no language" : names)
            .Append('.')
            .ToString();
    }

    /// <summary>Renders a chain, whose root page belongs to <paramref name="primary"/> or to the set itself.</summary>
    private static List<DocumentationPage> Render(DocumentationChain chain, LanguageProject? primary)
    {
        var pages = new List<DocumentationPage>
        {
            new("index.html", RootPage.Render(chain, primary)),
            new("assets/docs.css", DocumentationAssets.Css),
            new("assets/docs.js", DocumentationAssets.Js),
        };

        foreach (var each in chain.Languages)
        {
            var folder = DocumentationChain.Folder(each);

            pages.Add(new($"{folder}/index.html", LanguageIndexPage.Render(chain, each, $"{folder}/index.html")));
            pages.Add(new($"{folder}/aspects.html", AspectsPage.Render(chain, each, $"{folder}/aspects.html")));

            foreach (var concept in DocumentationChain.Own(each))
            {
                var path = $"{folder}/concepts/{concept.Key}.html";

                pages.Add(new(path, ConceptPage.Render(chain, each, concept, path)));
            }
        }

        return pages;
    }

    /// <summary>Writes <paramref name="pages"/>, creating every folder they need.</summary>
    private static int Write(IReadOnlyList<DocumentationPage> pages, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory, nameof(directory));

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        foreach (var page in pages)
        {
            var path = Path.Combine(directory, page.Path.Replace('/', Path.DirectorySeparatorChar));
            var folder = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(folder))
            {
                _ = Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, page.Text, encoding);
        }

        return pages.Count;
    }
}