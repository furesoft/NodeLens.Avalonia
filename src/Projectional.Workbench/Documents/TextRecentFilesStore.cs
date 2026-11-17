using System.Text;

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
    private readonly string _path;

    /// <summary>Stores the list in <paramref name="path"/>.</summary>
    /// <param name="path">The file to read and write, usually under the user's local application data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public TextRecentFilesStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <summary>The stored paths, in the order they were written.</summary>
    public IReadOnlyList<string> Read()
    {
        try
        {
            return File.Exists(_path)
                ? [.. File.ReadAllLines(_path, Encoding.UTF8)]
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Replaces what is stored.</summary>
    public void Write(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        try
        {
            if (Path.GetDirectoryName(_path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(_path, paths, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (IOException)
        {
            // A list of recent files is a convenience; losing it must not take the window with it.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}