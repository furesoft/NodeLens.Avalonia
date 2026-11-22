using System.Text;

using Projectional.LanguageExport;

namespace Projectional.Workbench;

/// <summary>Writes what an export produced into the folder a user picked.</summary>
/// <remarks>
/// <para>
/// The export answers text and bytes and never a path - the same rule text generation follows (open question 12) -
/// so writing is the shell's job, because the shell is the part that knows a user chose a folder. A language
/// component that wrote files would have to guess a path, an encoding and a newline convention, and a host that
/// embeds the editor could not decline.
/// </para>
/// <para>
/// It is a type of its own rather than a few lines in the window so the behaviour can be pinned headlessly: overwrite
/// rather than append, UTF-8 without a BOM, and a folder created when it does not exist.
/// </para>
/// </remarks>
internal static class LanguageExportWriter
{
    /// <summary>Writes the generated files and, when present, the assemblies into <paramref name="directory"/>.</summary>
    /// <param name="result">What the exporter produced.</param>
    /// <param name="directory">The folder to write into; it is created when it does not exist.</param>
    /// <returns>The written paths, the files in the export's order and the assemblies after them, base first.</returns>
    /// <exception cref="IOException">A file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be written to.</exception>
    public static IReadOnlyList<string> Write(ExportResult result, string directory)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        var written = new List<string>(result.Files.Count + result.Assemblies.Count);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        foreach (var file in result.Files)
        {
            var path = Path.Combine(directory, file.Name);

            File.WriteAllText(path, file.Text, encoding);
            written.Add(path);
        }

        foreach (var assembly in result.Assemblies)
        {
            var path = Path.Combine(directory, assembly.Name);

            File.WriteAllBytes(path, assembly.Bytes);
            written.Add(path);
        }

        return written;
    }
}
