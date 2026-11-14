using Projectional.Core;

namespace Projectional.Persistence;

/// <summary>
/// The file side of persistence: one model per file, written so that a reader either sees the old
/// file or the new one and never a half-written mixture.
/// </summary>
public static class ModelFile
{
    /// <summary>The extension a stored model has, matching what MPS calls a model file.</summary>
    public const string Extension = ".mps";

    /// <summary>Reads a model file and adds the model to <paramref name="project"/>.</summary>
    /// <exception cref="ModelFormatException">The file is not a model this build can read.</exception>
    public static Model Load(Project project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ModelXml.ReadFrom(project, stream);
    }

    /// <summary>
    /// Writes a model file, replacing whatever was there in one step.
    /// </summary>
    /// <remarks>
    /// The bytes go to a temporary file next to the target, are flushed to the disk, and only then
    /// take the target's place. A crash leaves the temporary file behind and the old model intact,
    /// which is the failure mode that lets a user keep working.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The model is in the middle of a transaction.</exception>
    public static void Save(Model model, string path)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target)
            ?? throw new ArgumentException($"'{path}' has no directory to save into.", nameof(path));

        Directory.CreateDirectory(directory);

        var temporary = Path.Combine(directory, $"{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ModelXml.WriteTo(model, stream);
                stream.Flush(flushToDisk: true);
            }

            // The rename is the only step that is visible from outside, and it is atomic.
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
