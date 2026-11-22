using System.IO.Compression;
using System.Text;

namespace Projectional.LanguageExport;

/// <summary>Packs what an export produced into one zip archive, so a language can be handed on as one file.</summary>
/// <remarks>
/// <para>
/// The archive is bytes and not a path, for the same reason an export is: what a language becomes is a value, and where
/// it goes is the host's decision - the shell writes the file a user picked the place for.
/// </para>
/// <para>
/// The archive is reproducible: the entries are in the export's order and carry a fixed timestamp, because the exporter
/// already promises that the same language twice is the same bytes. A zip that differed by the day it was built would
/// be a diff nobody made, exactly like a generated file that did.
/// </para>
/// </remarks>
public static class LanguagePackage
{
    /// <summary>The timestamp every entry carries, which is the one the zip format's earliest date allows.</summary>
    /// <remarks>
    /// 1980-01-01 is what a zip has to say when it knows nothing: the format stores a DOS date, and this is its zero -
    /// which is also the value a reproducible build puts there instead of the clock.
    /// </remarks>
    private static readonly DateTimeOffset FixedTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Packs the requested half of <paramref name="result"/> into a zip archive.</summary>
    /// <param name="result">What the exporter produced.</param>
    /// <param name="content">The sources, or the assemblies.</param>
    /// <returns>The archive's bytes, ready to be written where the user asked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="content"/> asks for assemblies and the export compiled none, because it was asked for
    /// <see cref="ExportOutput.Source"/>.
    /// </exception>
    public static byte[] Zip(ExportResult result, PackageContent content)
    {
        ArgumentNullException.ThrowIfNull(result);

        var entries = content switch
        {
            PackageContent.Assemblies => Assemblies(result),
            _ => Sources(result),
        };

        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);

                entry.LastWriteTime = FixedTimestamp;

                using var stream = entry.Open();

                stream.Write(bytes);
            }
        }

        return buffer.ToArray();
    }

    private static IEnumerable<(string Name, byte[] Bytes)> Sources(ExportResult result) =>
        result.Files.Select(file => (file.Name, Encoding.UTF8.GetBytes(file.Text)));

    private static IEnumerable<(string Name, byte[] Bytes)> Assemblies(ExportResult result)
    {
        if (result.Assemblies.Count == 0)
        {
            throw new InvalidOperationException(
                "The export compiled no assemblies, so there is nothing to package: export with ExportOutput.Assembly first.");
        }

        return result.Assemblies.Select(assembly => (assembly.Name, assembly.Bytes));
    }
}
