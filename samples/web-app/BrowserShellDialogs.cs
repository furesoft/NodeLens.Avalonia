using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

using Projectional.Avalonia;

namespace Projectional.Browser;

/// <summary>The shell's questions answered by the browser's own pickers and an in-page prompt.</summary>
/// <remarks>
/// A browser has no modal window, so the unsaved-changes question is drawn over the shell and awaited; the file
/// pickers are Avalonia's browser storage provider, and what they answer is copied into the virtual file system the
/// shell can read (see <see cref="BrowserFiles"/>).
/// </remarks>
internal sealed class BrowserShellDialogs : IShellDialogs
{
    /// <inheritdoc />
    public async Task<string?> ChooseFolderAsync(string title)
    {
        var folders = await Top().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        }).ConfigureAwait(true);

        return folders.Count == 0 ? null : await BrowserFiles.MaterializeAsync(folders[0]).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async Task<string?> ChooseFileToOpenAsync(string title, ShellFileType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var files = await Top().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [Filter(type)],
        }).ConfigureAwait(true);

        return files.Count == 0 ? null : await BrowserFiles.MaterializeAsync(files[0]).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public Task<string?> ChooseFileToSaveAsync(
        string title,
        string suggestedName,
        ShellFileType type,
        string? defaultExtension = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        // No picker: the browser's download is the save, and it is triggered when the shell reports the write
        // (`WorkbenchView.FileSaved`). Asking twice would name the file once and write it elsewhere.
        var name = suggestedName;

        if (!string.IsNullOrEmpty(defaultExtension)
            && !name.EndsWith('.' + defaultExtension, StringComparison.OrdinalIgnoreCase))
        {
            name += '.' + defaultExtension;
        }

        return Task.FromResult<string?>(BrowserFiles.PathForDownload(name));
    }

    /// <inheritdoc />
    public Task<UnsavedChoice> AskUnsavedAsync(string title) =>
        BrowserUnsavedPrompt.AskAsync(Shell(), title);

    /// <summary>The shell control, which the prompt is anchored to.</summary>
    private static Control Shell() =>
        Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } view }
            ? view
            : throw new InvalidOperationException("the shell is not attached to the page yet.");

    /// <summary>The top level the shell is in, which owns the pickers.</summary>
    private static TopLevel Top() =>
        TopLevel.GetTopLevel(Shell()) ?? throw new InvalidOperationException("the shell is not attached to a top level yet.");

    /// <summary>Turns the library's filter value into the toolkit's.</summary>
    private static FilePickerFileType Filter(ShellFileType type) =>
        new(type.Name) { Patterns = [.. type.Patterns] };
}
