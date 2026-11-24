using System.IO.Compression;

namespace Projectional.Browser;

/// <summary>Zips what the page holds and hands it to the browser as one download.</summary>
/// <remarks>
/// "The whole package" is a host notion: this page's documents are files in the .NET in-memory system, so the package
/// is those documents plus the sample languages written beside them. A desktop host has no such entry because
/// `ShellOptions.DownloadPackage` is what shows it, and the desktop never supplies it.
/// </remarks>
internal static class BrowserPackage
{
    private const string Name = "projectional-package.zip";

    /// <summary>Builds the archive and downloads it.</summary>
    internal static void Download()
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, Path.Combine(BrowserFiles.Root, "downloads"), "documents");
            Add(archive, Path.Combine(AppContext.BaseDirectory, "Examples"), "Examples");
        }

        BrowserFiles.DownloadBytes(Name, buffer.ToArray());
    }

    private static void Add(ZipArchive archive, string root, string prefix)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var entry = archive.CreateEntry($"{prefix}/{relative}", CompressionLevel.Optimal);

            using var output = entry.Open();
            using var input = File.OpenRead(file);

            input.CopyTo(output);
        }
    }
}
