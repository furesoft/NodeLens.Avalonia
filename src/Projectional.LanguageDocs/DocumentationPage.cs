namespace Projectional.LanguageDocs;

/// <summary>One file of the documentation: where it goes, and what it holds.</summary>
/// <param name="Path">The path inside the documentation folder, with <c>/</c> as the separator.</param>
/// <param name="Text">The file's text, which is UTF-8 without a byte order mark and uses LF.</param>
/// <remarks>
/// Values rather than files, because the same output has two callers: the folder command writes it, and a package puts
/// it into an archive. That is the split <c>LanguageExporter</c> already follows, and it is why a generator lives in a
/// library rather than in the shell.
/// </remarks>
public sealed record DocumentationPage(string Path, string Text);