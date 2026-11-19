namespace Projectional.LanguageExport;

/// <summary>One file an export produces: its name, relative to the export directory, and its text.</summary>
/// <remarks>
/// The export answers files rather than writing them, so a test can read what a language would become without a
/// directory, and a tool can put the same text wherever it likes. Writing is the host's business for the same reason
/// text generation is: the framework does not guess a path.
/// </remarks>
internal sealed record GeneratedFile(string Name, string Text);
