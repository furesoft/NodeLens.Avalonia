using System.Text;

namespace Projectional.Workbench;

/// <summary>Keeps one language module's settings in the module's own folder.</summary>
/// <remarks>
/// <para>
/// The settings belong to the module, so they travel with it: a folder copied to another machine brings its versions
/// and its documentation target along, which a file under the user's application data could not do.
/// </para>
/// <para>
/// Reading never throws - a missing file is the defaults, an unreadable one is the defaults - for the reason the recent
/// list gives: a convenience file must not cost a window. Writing is the one operation that reports failure, because
/// pressing Apply and being told nothing would be the shell claiming a write that did not happen.
/// </para>
/// </remarks>
internal sealed class ModuleSettingsStore
{
    private readonly string _path;

    /// <summary>Reads and writes the settings of the module in <paramref name="directory"/>.</summary>
    /// <param name="directory">The module's folder.</param>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty.</exception>
    public ModuleSettingsStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _path = System.IO.Path.Combine(directory, ModuleSettings.FileName);
    }

    /// <summary>The file the settings are kept in.</summary>
    internal string SettingsPath => _path;

    /// <summary>What the module says about itself, or the defaults when it says nothing.</summary>
    internal ModuleSettings Read()
    {
        try
        {
            return File.Exists(_path)
                ? ModuleSettings.From(File.ReadAllLines(_path, Encoding.UTF8))
                : ModuleSettings.Default;
        }
        catch (IOException)
        {
            return ModuleSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return ModuleSettings.Default;
        }
    }

    /// <summary>Writes <paramref name="settings"/>, replacing what the file held.</summary>
    /// <param name="settings">What to write.</param>
    /// <param name="error">Why it could not be written, or <see langword="null"/> on success.</param>
    /// <returns>Whether the file now holds <paramref name="settings"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// LF endings and UTF-8 without a BOM, so a module folder written on Windows and one written on Linux are the same
    /// bytes - the rule the model's own reader and writer are held to.
    /// </remarks>
    internal bool TryWrite(ModuleSettings settings, out string? error)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            File.WriteAllText(
                _path,
                string.Join('\n', settings.ToLines()) + '\n',
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            error = null;

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;

            return false;
        }
    }
}
