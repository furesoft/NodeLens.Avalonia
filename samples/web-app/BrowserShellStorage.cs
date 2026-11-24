using System.Runtime.InteropServices.JavaScript;

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

    [JSImport("getItem", "shell")]
    private static partial string? GetItem(string key);

    [JSImport("setItem", "shell")]
    private static partial void SetItem(string key, string value);
}
