using Avalonia.Controls;
using Avalonia.Platform.Storage;

using Projectional.Avalonia;

namespace Projectional.Workbench;

/// <summary>The dialogs the standalone app asks with: Avalonia's pickers and its own prompt.</summary>
/// <remarks>
/// <para>
/// This is the implementation a host <em>replaces</em>, and it is in the app rather than in the library precisely
/// because it is the one that needs Avalonia's toolkit: <c>StorageProvider</c> belongs to a window, and the prompt is
/// a window of its own. The library declares the questions; this answers them the desktop way.
/// </para>
/// <para>
/// It holds a window rather than a top level so the prompt has an owner - a dialog with no owner can be lost behind the
/// shell - and so a host that composes the shell in its own window keeps its own modality.
/// </para>
/// </remarks>
internal sealed class WindowShellDialogs : IShellDialogs
{
    private readonly Window _owner;

    /// <summary>Answers the shell's questions with <paramref name="owner"/>'s pickers and prompts.</summary>
    /// <param name="owner">The window the dialogs belong to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    public WindowShellDialogs(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
    }

    /// <inheritdoc />
    public async Task<string?> ChooseFolderAsync(string title)
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        }).ConfigureAwait(true);

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> ChooseFileToOpenAsync(string title, ShellFileType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [Filter(type)],
        }).ConfigureAwait(true);

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> ChooseFileToSaveAsync(
        string title,
        string suggestedName,
        ShellFileType type,
        string? defaultExtension = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = defaultExtension,
            FileTypeChoices = [Filter(type)],
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public Task<UnsavedChoice> AskUnsavedAsync(string title) =>
        UnsavedChangesPrompt.AskAsync(_owner, title);

    /// <summary>Turns the library's filter value into the toolkit's.</summary>
    /// <remarks>
    /// The translation lives here and only here: the interface cannot mention <see cref="FilePickerFileType"/> without
    /// making every host depend on Avalonia, so this adapter is the single place the two vocabularies meet.
    /// </remarks>
    private static FilePickerFileType Filter(ShellFileType type) =>
        new(type.Name) { Patterns = [.. type.Patterns] };
}
