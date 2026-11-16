using Projectional.Core;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Samples;

namespace Projectional.Workbench;

/// <summary>
/// Reads and writes the model a tab edits, and keeps the tab's idea of "saved" in step with the file.
/// </summary>
/// <remarks>
/// <para>
/// The window asks the user where a file is; this type is what actually touches the disk. Keeping the
/// two apart is what makes the interesting half testable without a file picker, and it puts the two
/// rules the shell must not get wrong in one place: a save marks the document clean only after the
/// write succeeded, and a load reports a broken file instead of throwing at the user.
/// </para>
/// <para>
/// Every model the workbench opens is loaded against a project the shell supplies, because the set of languages is
/// per shell and grows when a language project is opened ([language-projects.md](../../docs/architecture/language-projects.md)).
/// A file whose concepts are not in that registry is refused with a diagnostic rather than half-loaded, which is the
/// answer that tells the shell it forgot a language.
/// </para>
/// </remarks>
internal static class DocumentFile
{
    /// <summary>The extension the workbench opens and saves, including the dot.</summary>
    internal const string Extension = ModelFile.Extension;

    /// <summary>Reads the model in <paramref name="path"/> as a ruleset document.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The model, or <see langword="null"/> when the file is not a document this app can read.</returns>
    /// <param name="error">Why the file could not be read, or <see langword="null"/> on success.</param>
    /// <remarks>
    /// A malformed file and an unreadable one are the same answer to the user - the document did not
    /// open - so both come back as a message rather than as an exception the window would have to
    /// catch twice.
    /// </remarks>
    internal static Model? TryLoad(string path, Project project, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);

        try
        {
            error = null;

            return ModelFile.Load(project, path);
        }
        catch (Exception exception) when (exception is ModelFormatException or IOException or UnauthorizedAccessException)
        {
            error = exception.Message;

            return null;
        }
    }

    /// <summary>Writes <paramref name="document"/> to <paramref name="path"/> and marks it saved.</summary>
    /// <param name="document">The document to write.</param>
    /// <param name="path">The file to write.</param>
    /// <param name="error">Why the file could not be written, or <see langword="null"/> on success.</param>
    /// <returns>Whether the document reached the disk.</returns>
    /// <remarks>
    /// The tab is only marked saved once the write returned, so a failed save leaves the document
    /// dirty and the user's changes still recoverable.
    /// </remarks>
    internal static bool TrySave(DocumentTab document, string path, out string? error)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            ModelFile.Save(document.Model, path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;

            return false;
        }

        error = null;
        document.MarkSaved(path);

        return true;
    }
}
