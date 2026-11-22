namespace Projectional.LanguageExport;

/// <summary>What an export produced: the generated files, and the assemblies when they were asked for.</summary>
/// <param name="Files">
/// One source file per language, base first, and the README of the language that was exported last.
/// </param>
/// <param name="Assemblies">
/// One per language, base first - empty unless the caller asked for <see cref="ExportOutput.Assembly"/>, which is the
/// difference between reviewing what a language becomes and shipping it. Empty rather than null so that "not asked
/// for" and "asked for and got nothing" cannot be confused.
/// </param>
/// <remarks>
/// A language ships as one package per language, so the answer is a list even when the exported language stands on
/// nothing: that is the shape a chain produces, and the single assembly is a list with one entry rather than another
/// type. <see cref="Assembly"/> is the convenience for the language that was asked for - the last entry.
/// </remarks>
public sealed record ExportResult(IReadOnlyList<GeneratedFile> Files, IReadOnlyList<GeneratedAssembly> Assemblies)
{
    /// <summary>The assembly of the language that was exported, or <see langword="null"/> when none was compiled.</summary>
    /// <remarks>It is the last one, because the languages it stands on are exported before it, base first.</remarks>
    public GeneratedAssembly? Assembly => Assemblies.Count == 0 ? null : Assemblies[^1];
}
