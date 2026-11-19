namespace Projectional.LanguageExport;

/// <summary>What a caller wants back from an export: the source it can read, or the assembly as well.</summary>
/// <remarks>
/// Two outputs rather than two methods because the source is not an intermediate the assembly replaces: it is what
/// an author reviews, what a diff shows and what a package ships beside its binary. Asking for the assembly therefore
/// adds a result instead of removing one.
/// </remarks>
public enum ExportOutput
{
    /// <summary>The generated files alone - the C# and the README, nothing compiled.</summary>
    Source,

    /// <summary>The generated files and the assembly that source compiles to.</summary>
    Assembly,
}
