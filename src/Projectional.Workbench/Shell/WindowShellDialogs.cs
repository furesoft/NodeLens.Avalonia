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
/// It holds a way to reach the window rather than the window so the prompt has an owner - a dialog with no owner can be
/// lost behind the shell - and so a host that composes the shell in its own window keeps its own modality. The owner is
/// resolved when a question is asked, not when the shell is built, because a control has no top level until it is attached.
/// </para>
/// </remarks>
internal sealed class WindowShellDialogs : IShellDialogs
{
    private readonly Func<Window?> _owner;

    /// <summary>Answers the shell's questions with the pickers and prompts of the window it is hosted in.</summary>
    /// <param name="owner">Answers the window the dialogs belong to, or <see langword="null"/> before it is attached.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    public WindowShellDialogs(Func<Window?> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
    }

    /// <summary>The window the questions belong to, which every answer needs.</summary>
    private Window Owner =>
        _owner() ?? throw new InvalidOperationException(
            "the shell is not hosted in a window, so the desktop dialogs cannot answer; supply IShellDialogs in ShellOptions.");

    /// <inheritdoc />
    public async Task<string?> ChooseFolderAsync(string title)
    {
        var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
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

        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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

        var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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
        UnsavedChangesPrompt.AskAsync(Owner, title);

    /// <summary>Turns the library's filter value into the toolkit's.</summary>
    /// <remarks>
    /// The translation lives here and only here: the interface cannot mention <see cref="FilePickerFileType"/> without
    /// making every host depend on Avalonia, so this adapter is the single place the two vocabularies meet.
    /// </remarks>
    private static FilePickerFileType Filter(ShellFileType type) =>
        new(type.Name) { Patterns = [.. type.Patterns] };
}
