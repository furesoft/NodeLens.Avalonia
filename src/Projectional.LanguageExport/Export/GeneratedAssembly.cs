namespace Projectional.LanguageExport;

/// <summary>One compiled artefact an export produces: its file name, relative to the export directory, and its bytes.</summary>
/// <remarks>
/// Bytes rather than a path, for the same reason <see cref="GeneratedFile"/> answers text: where the result goes is
/// the host's decision, and a test can load what it was handed without touching a directory.
/// </remarks>
public sealed record GeneratedAssembly(string Name, byte[] Bytes);
