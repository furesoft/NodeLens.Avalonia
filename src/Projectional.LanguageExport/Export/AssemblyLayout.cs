namespace Projectional.LanguageExport;

/// <summary>How many assemblies an export compiles: one per language, or one holding all of them.</summary>
/// <remarks>
/// The sources are one file per language in both cases - a class per language is what a reader reviews and what a diff
/// shows. The choice is about the binary: a package per language lets a consumer take only the base it needs, and one
/// assembly is what a host that embeds a whole language family references. Neither is a default the other can replace,
/// so both are offered.
/// </remarks>
public enum AssemblyLayout
{
    /// <summary>One assembly per language, base first - the shape a package family ships in.</summary>
    PerLanguage,

    /// <summary>
    /// One assembly holding every language of the export, named after the language that was exported - the shape a host
    /// that takes the whole family references.
    /// </summary>
    /// <remarks>
    /// The languages share the assembly's <em>identity</em>: its name, its version and its informational version are the
    /// exported language's, and each dependency keeps its own name and version as <c>AssemblyMetadata</c>, so a reader
    /// can still tell which language contributed what. Two languages whose classes answer the same name are a compile
    /// error, which is the honest answer: one assembly cannot hold both.
    /// </remarks>
    Combined,
}
