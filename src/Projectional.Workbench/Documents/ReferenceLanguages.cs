using Projectional.Json;
using Projectional.Json.Checks;
using Projectional.Json.Intentions;
using Projectional.Toml;
using Projectional.Toml.Checks;
using Projectional.Toml.Intentions;
using Projectional.Xml;
using Projectional.Xml.Checks;
using Projectional.Xml.Intentions;

namespace Projectional.Workbench;

/// <summary>The reference languages the app ships: a JSON, XML or TOML file opens as a document of its own format.</summary>
/// <remarks>
/// <para>
/// They are the languages the framework uses to prove that a model can mirror a file, and the app is where that stops
/// being a claim: a user picks a file, gets a document, edits it, and generates it back. Nothing here is a second
/// definition - every member is the language package's own, so a fix in <c>Projectional.Json</c> is a fix in the app.
/// </para>
/// <para>
/// <b>Content decides, the extension is the fallback.</b> Every importer answers a cheap shape question (does the text
/// start like an object, an element or a pair), so the text is the better witness than the file name - a <c>.json</c>
/// file that actually holds XML is imported as XML, and a file with no extension at all still opens. The extension is
/// what decides when the text starts like none of them, which is exactly the case where the content said nothing.
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

    /// <summary>TOML, the format a settings file is usually written in.</summary>
    internal static ReferenceLanguage Toml { get; } = new(
        "TOML",
        "toml",
        ".toml",
        TomlLanguage.Importer,
        TomlLanguage.Concepts,
        TomlLanguage.Notations,
        TomlTextGenerators.All,
        TomlIntentions.Provider,
        TomlChecks.All);

    /// <summary>Every format the app can import, in the order a file dialog should offer them.</summary>
    internal static IReadOnlyList<ReferenceLanguage> All { get; } = [Json, Xml, Toml];

    /// <summary>Which language <paramref name="text"/> is, or <see langword="null"/> when none claims it.</summary>
    /// <param name="fileName">The name the file had, or <see langword="null"/> when it has none.</param>
    /// <param name="text">What the host read.</param>
    /// <remarks>
    /// A cheap shape question is not a parse, so two formats can answer yes to one text: a TOML header starts with a
    /// bracket, which is exactly how a JSON array starts. The content still decides first, and the file name is the
    /// tie-break between two claimers - only when the text starts like none of them is the extension the only answer
    /// left, which is the case a picker has to honour anyway.
    /// </remarks>
    internal static ReferenceLanguage? Detect(string? fileName, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<ReferenceLanguage> claims = [.. All.Where(language => language.Importer.CanImport(text))];

        var extension = fileName is null ? null : Path.GetExtension(fileName);
        var byExtension = extension is null
            ? null
            : All.FirstOrDefault(language => string.Equals(extension, language.Extension, StringComparison.OrdinalIgnoreCase));

        // One format claiming the text is the answer; the extension breaks a tie between two that both claim it, and is
        // the only witness when the text starts like none of them.
        if (claims.Count == 1)
        {
            return claims[0];
        }

        if (byExtension is not null && (claims.Count == 0 || claims.Contains(byExtension)))
        {
            return byExtension;
        }

        return claims.Count > 0 ? claims[0] : null;
    }
}
