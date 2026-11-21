using System.Text;

namespace Projectional.Avalonia;

/// <summary>The storage a desktop shell uses: one text file per path, a line each.</summary>
/// <remarks>
/// <para>
/// A text file rather than JSON: the content is a list of paths, nothing here needs a schema, and a file a user can
/// read and fix by hand is worth more than one that needs a parser to explain. Whatever comes back is validated by
/// the type that reads it, so a hand-edited file cannot break the menu.
/// </para>
/// <para>
/// Both failures are silent and answered with what is still true - see <see cref="IShellStorage"/> for why. The one
/// difference from a bare <c>File</c> call is that the directory is created on write, so a first run has somewhere to
/// put the file without the shell knowing where its state lives.
/// </para>
/// </remarks>
public sealed class FileShellStorage : IShellStorage
{
    /// <summary>The one instance: the type holds no state, so a host needs no second one.</summary>
    public static FileShellStorage Default { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> ReadLines(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return File.Exists(path) ? [.. File.ReadAllLines(path, Encoding.UTF8)] : [];
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

    /// <inheritdoc />
    public bool WriteLines(string path, IReadOnlyList<string> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(lines);

        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return true;
        }
        catch (IOException)
        {
            // A list of recent files is a convenience; losing it must not take the window with it.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
