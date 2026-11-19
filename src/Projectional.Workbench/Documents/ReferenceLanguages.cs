using Projectional.Json;
using Projectional.Json.Checks;
using Projectional.Json.Intentions;
using Projectional.Xml;
using Projectional.Xml.Checks;
using Projectional.Xml.Intentions;

namespace Projectional.Workbench;

/// <summary>The reference languages the app ships: a JSON file and an XML file open as documents of their own format.</summary>
/// <remarks>
/// <para>
/// They are the languages the framework uses to prove that a model can mirror a file, and the app is where that stops
/// being a claim: a user picks a file, gets a document, edits it, and generates it back. Nothing here is a second
/// definition - every member is the language package's own, so a fix in <c>Projectional.Json</c> is a fix in the app.
/// </para>
/// <para>
/// <b>Content decides, the extension is the fallback.</b> Both importers answer a cheap shape question (does the text
/// start like an object or an element), and the two answers cannot both be yes, so the text is the better witness than
/// the file name - a <c>.json</c> file that actually holds XML is imported as XML, and a file with no extension at all
/// still opens. The extension is what decides when the text starts like neither, which is exactly the case where the
/// content said nothing.
/// </para>
/// </remarks>
internal static class ReferenceLanguages
{
    /// <summary>JSON, in the order a menu should list it.</summary>
    internal static ReferenceLanguage Json { get; } = new(
        "JSON",
        "json",
        ".json",
        JsonLanguage.Importer,
        JsonLanguage.Concepts,
        JsonLanguage.Notations,
        JsonTextGenerators.All,
        JsonIntentions.Provider,
        JsonChecks.All);

    /// <summary>XML, in the order a menu should list it.</summary>
    internal static ReferenceLanguage Xml { get; } = new(
        "XML",
        "xml",
        ".xml",
        XmlLanguage.Importer,
        XmlLanguage.Concepts,
        XmlLanguage.Notations,
        XmlTextGenerators.All,
        XmlIntentions.Provider,
        XmlChecks.All);

    /// <summary>Every format the app can import, in the order a file dialog should offer them.</summary>
    internal static IReadOnlyList<ReferenceLanguage> All { get; } = [Json, Xml];

    /// <summary>Which language <paramref name="text"/> is, or <see langword="null"/> when none claims it.</summary>
    /// <param name="fileName">The name the file had, or <see langword="null"/> when it has none.</param>
    /// <param name="text">What the host read.</param>
    internal static ReferenceLanguage? Detect(string? fileName, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var language in All)
        {
            if (language.Importer.CanImport(text))
            {
                return language;
            }
        }

        if (fileName is not null)
        {
            var extension = Path.GetExtension(fileName);

            foreach (var language in All)
            {
                if (string.Equals(extension, language.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    return language;
                }
            }
        }

        return null;
    }
}
