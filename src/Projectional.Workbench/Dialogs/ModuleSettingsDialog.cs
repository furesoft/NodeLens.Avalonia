using Avalonia.Controls;
using Avalonia.Interactivity;

using Projectional.Languages;

namespace Projectional.Workbench;

/// <summary>
/// What a language module says about itself: its versions, what it is made of, what the shell lowered out of it, and
/// where its documentation goes.
/// </summary>
/// <remarks>
/// <para>
/// The pages are an inspector before they are an editor - almost everything here is read from the folder and from the
/// language the shell built - with three settings that are the author's rather than the folder's: the two version
/// numbers and the documentation target. Those three are what Apply writes, into
/// <see cref="ModuleSettings.FileName"/> beside the module's definitions, so a folder copied to another machine
/// carries them.
/// </para>
/// <para>
/// A failed write keeps the dialog open and says so. Closing on OK after a write the disk refused would be the shell
/// reporting a change that only exists in a control nobody will look at again.
/// </para>
/// </remarks>
internal sealed partial class ModuleSettingsDialog : Window
{
    private readonly ModuleSettingsStore _store;

    private ModuleSettingsDialog(ModuleFacts facts, ModuleSettingsStore store)
    {
        _store = store;

        InitializeComponent();

        Title = $"Module settings for {facts.Name}";

        NameBox.Text = facts.Name;
        PathBox.Text = facts.Directory;
        Roots.ItemsSource = facts.Roots;
        Dependencies.ItemsSource = facts.Aspects;
        Contents.ItemsSource = facts.Contents;

        // Read after the layout exists, so a settings file that is missing or damaged lands in the fields rather than in
        // a control that is about to be created.
        var settings = store.Read();

        LanguageVersion.Value = settings.LanguageVersion;
        ModuleVersion.Value = settings.ModuleVersion;
        DocumentationOutput.Text = settings.DocumentationOutput;
    }

    /// <summary>Shows the settings of <paramref name="module"/> over <paramref name="owner"/>.</summary>
    /// <param name="owner">The window the dialog belongs to.</param>
    /// <param name="module">The module whose folder is read.</param>
    /// <returns>Whether the dialog was closed with OK, i.e. whether anything may have been written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="module"/> is <see langword="null"/>.</exception>
    internal static async Task<bool> ShowAsync(Window owner, LanguageProject module)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(module);

        return await Create(owner, module).ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    /// <summary>Shows the settings of <paramref name="module"/> without waiting for an answer.</summary>
    /// <param name="owner">The window the dialog belongs to.</param>
    /// <param name="module">The module whose folder is read.</param>
    /// <returns>The dialog, which the caller closes.</returns>
    /// <remarks>
    /// Internal for the screenshot harness: a modal dialog cannot be photographed, because the call that opened it does
    /// not return until it is closed. It is the same dialog either way - only the way it is shown differs.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="module"/> is <see langword="null"/>.</exception>
    internal static ModuleSettingsDialog Open(Window owner, LanguageProject module)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(module);

        var dialog = Create(owner, module);

        dialog.Show(owner);

        return dialog;
    }

    /// <summary>The dialog for <paramref name="module"/>, not yet shown.</summary>
    private static ModuleSettingsDialog Create(Window owner, LanguageProject module)
    {
        var dialog = new ModuleSettingsDialog(
            ModuleFacts.Of(module),
            new ModuleSettingsStore(module.Directory));

        // A dialog that ignored the variant the window follows would flash white over a dark shell.
        dialog.RequestedThemeVariant = owner.RequestedThemeVariant;

        return dialog;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (Apply())
        {
            Close(true);
        }
    }

    private void OnApply(object? sender, RoutedEventArgs e) => Apply();

    /// <summary>Writes the three settings that are the author's, and says what happened.</summary>
    /// <returns>Whether the file now holds them.</returns>
    private bool Apply()
    {
        var settings = new ModuleSettings(
            (int)(LanguageVersion.Value ?? ModuleSettings.DefaultVersion),
            (int)(ModuleVersion.Value ?? ModuleSettings.DefaultVersion),
            DocumentationOutput.Text?.Trim() ?? string.Empty);

        if (!_store.TryWrite(settings, out var error))
        {
            Status.Text = $"The settings could not be written: {error}";

            return false;
        }

        Status.Text = $"Written to {_store.SettingsPath}";

        return true;
    }
}
