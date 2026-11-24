using System.Runtime.InteropServices.JavaScript;

using Avalonia.Platform.Storage;

namespace Projectional.Browser;

/// <summary>Brings files the user picked into the .NET virtual file system, and sends saved files back out.</summary>
/// <remarks>
/// <para>
/// Avalonia's browser backend implements <see cref="IStorageProvider"/>, so the pickers work, but a picked file has no
/// local path: it is a handle to a browser blob. The shell speaks paths, so the file is copied into the runtime's
/// in-memory file system once, and that copy is what the shell opens.
/// </para>
/// <para>
/// Saving is the other direction: the shell writes its file into the virtual system, and the host sends those bytes to
/// the browser as a download. Nothing here writes outside the page - the in-memory system dies with the tab.
/// </para>
/// </remarks>
internal static partial class BrowserFiles
{
    /// <summary>Where the browser head keeps what it copies and what it writes.</summary>
    internal static string Root { get; } = Path.Combine(Path.GetTempPath(), "projectional-browser");

    /// <summary>Copies <paramref name="file"/> into the virtual file system and answers the path it now has.</summary>
    internal static async Task<string> MaterializeAsync(IStorageFile file)
    {
        var folder = Path.Combine(Root, "open", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        return await CopyAsync(file, Path.Combine(folder, file.Name));
    }

    /// <summary>Copies a whole language folder, so the shell can open it as a project.</summary>
    internal static async Task<string> MaterializeAsync(IStorageFolder folder)
    {
        var root = Path.Combine(Root, "languages", Guid.NewGuid().ToString("N"), folder.Name);

        await CopyAsync(folder, root);

        return root;
    }

    /// <summary>The path a saved file is written to before it is downloaded.</summary>
    internal static string PathForDownload(string name)
    {
        var safe = string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var folder = Path.Combine(Root, "downloads");

        Directory.CreateDirectory(folder);

        return Path.Combine(folder, safe);
    }

    /// <summary>Sends the file at <paramref name="path"/> to the browser as a download.</summary>
    internal static void Download(string path)
    {
        if (File.Exists(path))
        {
            DownloadBytes(Path.GetFileName(path), File.ReadAllBytes(path));
        }
    }

    /// <summary>Sends bytes to the browser as a download, which is what "save" means here.</summary>
    internal static void DownloadBytes(string name, byte[] content) =>
        Save(name, Convert.ToBase64String(content));

    private static async Task<string> CopyAsync(IStorageFile file, string path)
    {
        await using var input = await file.OpenReadAsync();
        await using var output = File.Create(path);

        await input.CopyToAsync(output);

        return path;
    }

    private static async Task CopyAsync(IStorageFolder folder, string path)
    {
        Directory.CreateDirectory(path);

        await foreach (var item in folder.GetItemsAsync().ConfigureAwait(true))
        {
            switch (item)
            {
                case IStorageFolder child:
                    await CopyAsync(child, Path.Combine(path, child.Name));
                    break;

                case IStorageFile file:
                    await CopyAsync(file, Path.Combine(path, file.Name));
                    break;
            }
        }
    }

    [JSImport("download", "shell")]
    private static partial void Save(string name, string base64);
}
