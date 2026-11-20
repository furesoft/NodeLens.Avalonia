using System.Globalization;

namespace Projectional.Workbench;

/// <summary>
/// What a language module says about itself beyond its definitions: the two version numbers it carries and the
/// folder its documentation is written to.
/// </summary>
/// <remarks>
/// <para>
/// A module is a folder of definition documents, so its settings are the one thing about it that is neither a concept
/// nor a notation. They are kept as a value rather than as fields on <see cref="Projectional.Languages.LanguageProject"/>,
/// because a project is what the definitions turned out to be while these are what the author declared about them - the
/// same split the recent list follows.
/// </para>
/// <para>
/// The spelling is a line per setting, <c>key = value</c>. A file a user can open and fix is worth more than one that
/// needs a parser to explain, and the format is read tolerantly: an unknown key, a missing number or a damaged line is
/// the default for that setting rather than a module that will not open. <see cref="ToLines"/> is the one writer, so
/// the file cannot drift from what the reader understands.
/// </para>
/// </remarks>
internal sealed record ModuleSettings(int LanguageVersion, int ModuleVersion, string DocumentationOutput)
{
    /// <summary>The name of the file a module's settings are kept in, inside the module's folder.</summary>
    internal const string FileName = "module.settings";

    private const string LanguageVersionKey = "language.version";
    private const string ModuleVersionKey = "module.version";
    private const string DocumentationOutputKey = "documentation.output";

    /// <summary>The version a module that has never been given one carries.</summary>
    /// <remarks>
    /// Zero, because a version is what the author says it is and nothing has been said yet. Inventing <c>1.0</c> would
    /// put a claim in the file that nobody made.
    /// </remarks>
    internal const int DefaultVersion = 0;

    /// <summary>The settings of a module that has not written any.</summary>
    internal static ModuleSettings Default { get; } = new(DefaultVersion, DefaultVersion, string.Empty);

    /// <summary>Reads the settings a file holds, or the defaults for whatever it does not say.</summary>
    /// <param name="lines">The file's lines, in order.</param>
    /// <remarks>
    /// A line without <c>=</c> is skipped rather than refused, and so is a key this version does not know: a settings
    /// file is written by one version of a program and read by the next, and refusing an unfamiliar line would make
    /// that upgrade a module nobody can open.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    internal static ModuleSettings From(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var language = DefaultVersion;
        var module = DefaultVersion;
        var documentation = string.Empty;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var split = trimmed.IndexOf('=');

            if (split <= 0)
            {
                continue;
            }

            var key = trimmed[..split].Trim();
            var value = trimmed[(split + 1)..].Trim();

            switch (key)
            {
                case LanguageVersionKey:
                    language = Number(value, DefaultVersion);
                    break;
                case ModuleVersionKey:
                    module = Number(value, DefaultVersion);
                    break;
                case DocumentationOutputKey:
                    documentation = value;
                    break;
            }
        }

        return new ModuleSettings(language, module, documentation);
    }

    /// <summary>What a settings file says, in the order it is written.</summary>
    /// <remarks>
    /// The same keys in the same order every time, so a module whose settings did not change produces the same bytes -
    /// the rule the model's own writer follows, for the same reason: a file that differs by nothing shows up as a
    /// change.
    /// </remarks>
    internal IReadOnlyList<string> ToLines() =>
    [
        "# What this language module says about itself. Written by Module settings.",
        $"{LanguageVersionKey} = {LanguageVersion.ToString(CultureInfo.InvariantCulture)}",
        $"{ModuleVersionKey} = {ModuleVersion.ToString(CultureInfo.InvariantCulture)}",
        $"{DocumentationOutputKey} = {DocumentationOutput}",
    ];

    private static int Number(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : fallback;
}
