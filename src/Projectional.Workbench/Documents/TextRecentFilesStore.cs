using Projectional.Avalonia;

namespace Projectional.Workbench;

/// <summary>Keeps the recent list in one text file, a path per line.</summary>
/// <remarks>
/// <para>
/// A text file rather than JSON: the content is a list of paths, nothing here needs a schema, and a file a user can read
/// and fix by hand is worth more than one that needs a parser to explain. <see cref="RecentFiles.From"/> validates what
/// comes back, so a hand-edited file cannot break the menu.
/// </para>
/// <para>
/// Every failure is silent and answered with what is still true: a missing file is an empty list, an unreadable one is
/// an empty list, and a failed write leaves the in-memory list alone. A shell that refused to start because its own
/// convenience file was locked would be a shell that lost the user's work to keep a menu.
/// </para>
/// </remarks>
internal sealed class TextRecentFilesStore
{
    private readonly IShellStorage _storage;
    private readonly string _path;

    /// <summary>Stores the list in <paramref name="path"/>, through the shell's storage seam.</summary>
    /// <param name="storage">Where the bytes go, which a host may answer differently.</param>
    /// <param name="path">The file to read and write, usually under the user's local application data.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public TextRecentFilesStore(IShellStorage storage, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(storage);

        _storage = storage;
        _path = path;
    }

    /// <summary>The stored paths, in the order they were written.</summary>
    public IReadOnlyList<string> Read() => _storage.ReadLines(_path);

    /// <summary>Replaces what is stored.</summary>
    /// <remarks>
    /// The answer is dropped on purpose: a recent list that could not be written is a menu that is shorter than the
    /// user expected, not a failure worth interrupting them for. <see cref="IShellStorage"/> already answers "what is
    /// still true" rather than throwing, so there is nothing here to catch.
    /// </remarks>
    public void Write(IReadOnlyList<string> paths) => _ = _storage.WriteLines(_path, paths);
}