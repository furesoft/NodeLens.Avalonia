namespace Projectional.LanguageExport;

/// <summary>What an export produced: the generated files, and the assembly when one was asked for.</summary>
/// <remarks>
/// <see cref="Assembly"/> is null unless the caller asked for <see cref="ExportOutput.Assembly"/>, which is the
/// difference between reviewing what a language becomes and shipping it. It is a null and not an empty result so that
/// "not asked for" and "asked for and got nothing" cannot be confused.
/// </remarks>
public sealed record ExportResult(IReadOnlyList<GeneratedFile> Files, GeneratedAssembly? Assembly);
