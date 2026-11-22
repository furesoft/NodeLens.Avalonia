namespace Projectional.LanguageExport;

/// <summary>What a package holds: the generated sources, or the assemblies they compile to.</summary>
/// <remarks>
/// Two answers rather than one archive holding both, because the two are handed to different people: sources travel to
/// whoever reviews or rebuilds a language, assemblies to whoever runs it. An archive with both would make "which of
/// these do I ship" a question again.
/// </remarks>
public enum PackageContent
{
    /// <summary>One C# file per language in the export, and the README that explains them.</summary>
    Sources,

    /// <summary>One assembly per language in the export, and nothing else.</summary>
    Assemblies,
}
