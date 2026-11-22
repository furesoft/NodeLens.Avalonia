using Projectional.Core;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Toolkit;

namespace Projectional.LanguageDocs;

/// <summary>The documents a language folder ships, drawn as text so a page can show what the language looks like.</summary>
/// <remarks>
/// <para>
/// Two sources, and both are the language's own: the accessory documents it declares in <c>accessory/</c>, which are
/// models <em>of</em> the language and already loaded, and the documents saved beside <c>language.mps</c>, which are what
/// a user opens. That is the same rule the app follows when it opens a language's documents, so a page and a window do
/// not disagree about what a document is.
/// </para>
/// <para>
/// The picture comes from <see cref="LanguageSnapshot"/>, the public half of the headless renderer, so what the page
/// shows is the layout the editor would produce, coloured by the same classification the editor paints with. The width is fixed rather than measured:
/// the output has to be byte-stable, and a width taken from a font would make it depend on the machine.
/// </para>
/// </remarks>
internal static class ExampleDocuments
{
    /// <summary>The columns a drawing gets, which is what fits a page without sideways scrolling.</summary>
    internal const int Width = 96;

    /// <summary>Every document <paramref name="language"/> ships, ordered by name.</summary>
    internal static IReadOnlyList<DocumentDrawing> Of(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        var drawings = new List<DocumentDrawing>();

        foreach (var model in language.Accessory)
        {
            drawings.Add(Draw(model.Name ?? "accessory", () => HighlightedText.Of(LanguageSnapshot.Runs(model, language.Notations, Width))));
        }

        if (language.Directory.Length == 0)
        {
            return [.. drawings.OrderBy(drawing => drawing.Name, StringComparer.Ordinal)];
        }

        foreach (var file in Enumerate(language.Directory))
        {
            var name = Path.GetFileNameWithoutExtension(file);

            drawings.Add(Draw(name, () => HighlightedText.Of(LanguageSnapshot.Runs(Load(language, file), language.Notations, Width))));
        }

        return [.. drawings.OrderBy(drawing => drawing.Name, StringComparer.Ordinal)];
    }

    /// <summary>The documents beside <c>language.mps</c>: top level, and not the language itself.</summary>
    private static IEnumerable<string> Enumerate(string directory) =>
        Directory.Exists(directory)
            ? Directory
                .EnumerateFiles(directory, "*.mps", SearchOption.TopDirectoryOnly)
                .Where(file => !string.Equals(Path.GetFileName(file), LanguageProject.LanguageFile, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal)
            : [];

    /// <summary>Loads one document into a project of its own, which is where its node ids are unique.</summary>
    private static Model Load(LanguageProject language, string file) =>
        ModelFile.Load(language.NewProject(), file);

    /// <summary>Draws one document, or records why it could not be drawn.</summary>
    private static DocumentDrawing Draw(string name, Func<string> render)
    {
        try
        {
            return new DocumentDrawing(name, render(), Refusal: null);
        }
        catch (Exception exception) when (exception is ModelFormatException
            or ConceptDefinitionException
            or IOException
            or UnauthorizedAccessException)
        {
            return new DocumentDrawing(name, Html: string.Empty, exception.Message);
        }
    }
}