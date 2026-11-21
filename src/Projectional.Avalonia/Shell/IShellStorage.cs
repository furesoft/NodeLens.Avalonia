namespace Projectional.Avalonia;

/// <summary>Where the shell keeps the little it persists, so a host can keep it somewhere else.</summary>
/// <remarks>
/// <para>
/// ADR 0015's rule is that anything the shell persists goes through this seam. It is deliberately small: what the
/// shell stores is a list of paths (recent files) and a small settings file, both of them lines of text at a known
/// place. A wider interface - "save this document", "list this project" - would be the shell's document model
/// leaking into the host, and the host would have to implement rules it did not make.
/// </para>
/// <para>
/// Every method answers what is still true rather than throwing: a convenience file that cannot be read leaves an
/// empty list, and one that cannot be written leaves the previous content alone. A shell that refused to start
/// because its own recent list was locked would be a shell that lost the user's work to keep a menu.
/// </para>
/// <para>
/// The paths are given by the shell, so a host that keeps its state in a database can ignore them - or read them as
/// keys. That is enough for the settings the shell owns; a document's own location is the user's choice and is asked
/// through <c>IShellDialogs</c> when it arrives.
/// </para>
/// </remarks>
public interface IShellStorage
{
    /// <summary>The lines stored at <paramref name="path"/>, or an empty list when there are none or they cannot be read.</summary>
    /// <param name="path">Where the shell keeps the file; the shell chooses it, the host decides what it means.</param>
    IReadOnlyList<string> ReadLines(string path);

    /// <summary>Replaces the lines stored at <paramref name="path"/>.</summary>
    /// <param name="path">Where the shell keeps the file.</param>
    /// <param name="lines">The content to store, in order.</param>
    /// <returns><see langword="true"/> when it was stored, <see langword="false"/> when it could not be.</returns>
    bool WriteLines(string path, IReadOnlyList<string> lines);
}
