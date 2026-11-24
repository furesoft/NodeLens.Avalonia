using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

using Projectional.Avalonia;

namespace Projectional.Browser;

/// <summary>The shell's small state - recent files, preferences - kept in the browser's localStorage.</summary>
/// <remarks>
/// The interface speaks of paths because the desktop keeps lines in a file; in a browser the path is only a key, which
/// is exactly what <see cref="IShellStorage"/> says a host may do with it. A page reload therefore keeps the recent
/// list, while the models themselves live in the .NET in-memory file system and are downloaded on save.
/// </remarks>
internal sealed partial class BrowserShellStorage : IShellStorage
{
    private const string Prefix = "projectional:";

    private const string FilePrefix = "projectional:file:";

    /// <inheritdoc />
    public IReadOnlyList<string> ReadLines(string path) =>
        GetItem(Prefix + path) is { Length: > 0 } text
            ? text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            : [];

    /// <inheritdoc />
    public bool WriteLines(string path, IReadOnlyList<string> lines)
    {
        SetItem(Prefix + path, string.Join('\n', lines));

        return true;
    }

    /// <summary>Stores the file at <paramref name="path"/>, so a reload can restore it.</summary>
    /// <remarks>
    /// Called after the shell reports a save. Base64 rather than text because the same seam carries generated text and
    /// a model - and a model is UTF-8 text, so both survive the round trip.
    /// </remarks>
    internal static void SaveFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                SetItem(FilePrefix + path, Convert.ToBase64String(File.ReadAllBytes(path)));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A convenience that cannot be read back must not fail the save the user asked for.
        }
    }

    /// <summary>Writes every stored file back where it was, before the shell reads its examples.</summary>
    internal static void RestoreFiles()
    {
        if (JsonSerializer.Deserialize<string[]>(Keys()) is not { } keys)
        {
            return;
        }

        foreach (var key in keys)
        {
            if (!key.StartsWith(FilePrefix, StringComparison.Ordinal) || GetItem(key) is not { } stored)
            {
                continue;
            }

            try
            {
                var path = key[FilePrefix.Length..];

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, Convert.FromBase64String(stored));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
            {
                // One unreadable entry costs that document, not the page: the rest still restore.
            }
        }
    }

    [JSImport("getItem", "shell")]
    private static partial string? GetItem(string key);

    [JSImport("setItem", "shell")]
    private static partial void SetItem(string key, string value);

    [JSImport("keys", "shell")]
    private static partial string Keys();
}
