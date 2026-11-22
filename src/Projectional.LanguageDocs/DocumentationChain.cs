using System.Text;

using Projectional.Core;
using Projectional.Languages;

namespace Projectional.LanguageDocs;

/// <summary>The languages one documentation set covers: the language itself and everything it stands on.</summary>
/// <remarks>
/// <para>
/// Base first, which is the order <see cref="LanguageProject.Resolved"/> already promises and an export already walks.
/// The chain is followed downward only: what depends on <em>this</em> language is the whole project and would be
/// unbounded, while what it stands on is a finite, already-resolved list.
/// </para>
/// <para>
/// Unlike an export, a shipped language is included: the exporter skips it because there is no folder of a user's to
/// write a package into, while a reader who follows a reference into a shipped vocabulary needs the page that
/// describes it.
/// </para>
/// </remarks>
internal sealed class DocumentationChain
{
    private readonly Dictionary<string, LanguageProject> _folders = new(StringComparer.Ordinal);
    private readonly List<LanguageProject> _languages = [];

    /// <summary>The chain of one language: the language and everything it stands on.</summary>
    internal DocumentationChain(LanguageProject language)
        : this([language])
    {
    }

    /// <summary>The chain of a whole documentation set: several languages, each of them once.</summary>
    /// <param name="languages">The languages to document, in any order.</param>
    /// <remarks>
    /// A site documents several languages at once, and doing it as one set rather than one set per language is what
    /// keeps a shared base to <em>one</em> folder: a base documented inside every dependent's set would be the same
    /// concept on several pages that can disagree, and a reader who followed a reference would land in a copy.
    /// <para>
    /// The order stays base first, because a language's own <see cref="LanguageProject.Resolved"/> already is and a
    /// language is taken only after its bases - so the listing reads as the layering it is.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="languages"/> is empty, or two of its languages call themselves the same thing while living in
    /// different folders - they would share one folder and overwrite each other's pages.
    /// </exception>
    internal DocumentationChain(IReadOnlyList<LanguageProject> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);

        if (languages.Count == 0)
        {
            throw new ArgumentException("A documentation set needs at least one language.", nameof(languages));
        }

        foreach (var each in languages)
        {
            ArgumentNullException.ThrowIfNull(each);

            foreach (var basis in each.Resolved)
            {
                Take(basis);
            }

            Take(each);
        }

        // The brand in the header names the set. For one language that is its name, because a reader who exported a
        // language's documentation is reading about that language; for several there is no one name to use.
        Title = _languages.Count == 1 ? _languages[0].Name : "Language documentation";
    }

    /// <summary>Every language the documentation covers, base first.</summary>
    internal IReadOnlyList<LanguageProject> Languages => _languages;

    /// <summary>What the whole tree is called, which the header of every page names.</summary>
    internal string Title { get; }

    /// <summary>The folder a language's pages live in, derived from its name.</summary>
    internal static string Folder(LanguageProject language) => Folder(language.Name);

    /// <summary>A name as a folder name: letters, digits, dots, dashes and underscores survive.</summary>
    internal static string Folder(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var safe = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            safe.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-');
        }

        return safe.Length == 0 ? "language" : safe.ToString();
    }

    /// <summary>The concepts a language declares itself, in key order.</summary>
    /// <remarks>
    /// Not <see cref="LanguageProject.Concepts"/>: that registry is the merged vocabulary, so a language that stands on
    /// another holds every one of the base's concepts too and a page per language would document the same concept in
    /// every folder that can see it. What a folder documents is what its own <c>structure/</c> declares - and each
    /// concept is resolved against the merged registry, so its slots carry the inherited shape.
    /// </remarks>
    internal static IReadOnlyList<Concept> Own(LanguageProject language) =>
    [
        .. language.Declarations
            .Select(declaration => language.Concepts.Get(declaration.Key))
            .OrderBy(concept => concept.Key, StringComparer.Ordinal),
    ];

    /// <summary>Whether a language's own declarations name <paramref name="conceptKey"/>.</summary>
    internal static bool Declares(LanguageProject language, string conceptKey) =>
        language.Declarations.Any(declaration => string.Equals(declaration.Key, conceptKey, StringComparison.Ordinal));

    /// <summary>The language that declares <paramref name="conceptKey"/>, or <see langword="null"/> when none does.</summary>
    /// <remarks>
    /// The most derived language wins, because that is the one whose registry answered: a language that re-declares a
    /// base's key means its own, and a page that linked to the base would describe the wrong concept.
    /// </remarks>
    internal LanguageProject? Owner(string conceptKey)
    {
        for (var index = _languages.Count - 1; index >= 0; index--)
        {
            if (Declares(_languages[index], conceptKey))
            {
                return _languages[index];
            }
        }

        return null;
    }

    /// <summary>A link from a page at <paramref name="depth"/> into a language's page.</summary>
    /// <param name="depth">How many folders deep the linking page sits: 0 at the root, 1 in a language, 2 in concepts.</param>
    /// <param name="language">The language whose page is the target.</param>
    /// <param name="file">The file inside that language's folder.</param>
    /// <remarks>
    /// Every link is written from the root and then walks back out, so one rule covers all three depths and a page
    /// cannot name a path the tree does not have. The walk is <c>../</c> rather than a leading slash, because the
    /// output is a folder a reader opens from disk as often as from a server.
    /// </remarks>
    internal static string Link(int depth, LanguageProject language, string file) =>
        $"{Up(depth)}{Folder(language)}/{file}";

    /// <summary>Whether a concept key is one the framework gives every registry.</summary>
    internal static bool IsFramework(string conceptKey) =>
        FrameworkInterfaces.All.Any(declaration => string.Equals(declaration.Key, conceptKey, StringComparison.Ordinal));

    /// <summary>A concept key as every page writes it: a link, or plain text when nothing writes a page for it.</summary>
    /// <remarks>
    /// <c>Framework.BaseConcept</c> is implemented by every concept and declared by no folder - the registry injects it -
    /// so a link would point at a page no one writes. It is drawn as plain text rather than as an unresolved reference,
    /// because nothing is missing: the framework's vocabulary is the one thing a reader never has to look up.
    /// </remarks>
    internal string ConceptReference(int depth, string conceptKey) =>
        ConceptLink(depth, conceptKey) is { } href
            ? Html.Link(href, conceptKey)
            : IsFramework(conceptKey)
                ? $"<code>{Html.Escape(conceptKey)}</code>"
                : $"<span class=\"unresolved\">{Html.Escape(conceptKey)}</span>";

    /// <summary>The link to a concept's page, or <see langword="null"/> when no language of the chain declares it.</summary>
    private string? ConceptLink(int depth, string conceptKey) =>
        Owner(conceptKey) is { } owner ? Link(depth, owner, $"concepts/{conceptKey}.html") : null;

    /// <summary>A link to the root page, which is where the search and the set live.</summary>
    internal static string HomeLink(int depth) => $"{Up(depth)}index.html";

    /// <summary>A link to the assets folder.</summary>
    internal static string AssetLink(int depth, string file) => $"{Up(depth)}assets/{file}";

    /// <summary>The walk back to the root from a page <paramref name="depth"/> folders deep.</summary>
    private static string Up(int depth) => string.Concat(Enumerable.Repeat("../", depth));

    /// <summary>Takes <paramref name="language"/> once, and refuses a second language of the same name.</summary>
    private void Take(LanguageProject language)
    {
        var folder = Folder(language);

        if (_folders.TryGetValue(folder, out var existing))
        {
            // The same language reached twice - a base two dependents share, or a caller that listed it twice - is the
            // ordinary case and costs nothing. Two different folders claiming one name is a tree that would silently
            // lose a page, so it fails here rather than in the browser.
            if (!string.Equals(existing.Directory, language.Directory, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Two languages of this documentation set are called '{language.Name}': '{existing.Directory}' and '{language.Directory}'.",
                    nameof(language));
            }

            return;
        }

        _folders[folder] = language;
        _languages.Add(language);
    }
}