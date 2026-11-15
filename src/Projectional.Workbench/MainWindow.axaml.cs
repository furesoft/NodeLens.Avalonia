using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;

using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Samples;

namespace Projectional.Workbench;

/// <summary>
/// The workbench window: a menu, a document tab strip and a status bar around the editor.
/// </summary>
/// <remarks>
/// <para>
/// The window is wiring and nothing else. It owns the window-level decisions a control cannot make -
/// which documents are open, which one is in front, what the status bar says, when a session is
/// disposed - and every one of them is expressed through the public API a host would use. There is no
/// reach into the projection or editing layers here, which is the point of shipping an app at all:
/// anything this window cannot do is a gap in the public surface rather than a private convenience.
/// </para>
/// <para>
/// A session belongs to a document, so closing a document disposes it and closing the window disposes
/// whatever is still open. That rule is the one hosts get wrong most often, so the shell demonstrates
/// it rather than describing it.
/// </para>
/// <para>
/// Each tab carries its own editor control rather than one control shared by all of them, because a
/// control draws the caret, the selection and the scroll offset of the session it shows; a shared
/// control would have to be reset on every switch and would forget where the user was reading.
/// </para>
/// </remarks>
public sealed partial class MainWindow : Window, IDisposable
{
    private readonly DocumentSet _documents = new();
    private readonly Dictionary<DocumentTab, TabItem> _tabs = [];

    private SyntaxTheme _theme = SyntaxTheme.Light;
    private string? _error;
    private bool _closeConfirmed;
    private bool _disposed;

    /// <summary>Creates the window and opens the sample document in it.</summary>
    public MainWindow()
    {
        InitializeComponent();

        ApplyTheme(_theme);
        OpenSample();

        Documents.SelectionChanged += OnSelectionChanged;
        Opened += (_, _) => FocusActiveDocument();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        Dispose();

        base.OnClosed(e);
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_closeConfirmed || !_documents.AnyUnsaved)
        {
            return;
        }

        // Closing is not something a prompt can interrupt: the window is already on its way out, so the
        // close is cancelled and re-issued once every dirty document has been dealt with.
        e.Cancel = true;
        Dispatcher.UIThread.Post(ConfirmClose);
    }

    private async void ConfirmClose()
    {
        foreach (var document in _documents.Unsaved)
        {
            if (!await ResolveUnsavedAsync(document))
            {
                return;
            }
        }

        _closeConfirmed = true;
        Close();
    }

    /// <summary>Closes every open document and disposes its session.</summary>
    /// <remarks>
    /// The session, not the control, owns the undo history, so the window disposes the sessions of the
    /// documents still open when it closes. It is idempotent because a host that embeds the window may
    /// dispose it explicitly as well as closing it.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _documents.Dispose();
        _tabs.Clear();
    }

    private void OpenSample()
    {
        var model = PayrollSample.NewSampleModel();

        OpenDocument(model, RulesLanguage.CreateNotations(model.Concepts), emphasis: RulesLanguage.Emphasis);
    }

    private void OpenDocument(
        Model model,
        NotationRegistry notations,
        string? filePath = null,
        ICellEmphasis? emphasis = null)
    {
        if (_documents.Find(model) is { } alreadyOpen)
        {
            Select(alreadyOpen);

            return;
        }

        var editor = new ProjectionalEditorControl();
        var document = new DocumentTab(model, notations, editor.TextMeasure, filePath, emphasis);
        editor.Session = document.Session;
        ApplyThemeTo(editor, _theme);

        // The header has to say "modified" without the user saving to find out, so the model itself
        // is the notification: an edit anywhere in it changes the dirty state.
        model.Changed += OnDocumentChanged;

        var tab = new TabItem { Header = document.Title, Content = editor };
        _documents.Add(document);
        _tabs.Add(document, tab);

        Documents.Items.Add(tab);
        Documents.SelectedItem = tab;

        UpdateStatus();
    }

    private async void CloseActiveDocument()
    {
        if (_documents.Active is not { } document)
        {
            return;
        }

        if (!await ResolveUnsavedAsync(document))
        {
            return;
        }

        Close(document);
    }

    private void Close(DocumentTab document)
    {
        document.Model.Changed -= OnDocumentChanged;
        _tabs.Remove(document, out var tab);
        _documents.Remove(document);

        if (tab is not null)
        {
            Documents.Items.Remove(tab);
        }

        if (_documents.Active is { } next)
        {
            Select(next);
        }

        UpdateStatus();
    }

    /// <summary>
    /// Asks what to do about <paramref name="document"/>'s unsaved changes.
    /// </summary>
    /// <param name="document">The document about to be closed.</param>
    /// <returns>Whether the caller may carry on; <see langword="false"/> means the user cancelled.</returns>
    /// <remarks>
    /// A save that fails answers "do not carry on": the user asked to keep the changes and they are
    /// still only in memory, so closing would lose exactly what they said to keep.
    /// </remarks>
    private async Task<bool> ResolveUnsavedAsync(DocumentTab document)
    {
        if (!document.IsDirty)
        {
            return true;
        }

        Select(document);

        return await UnsavedChangesPrompt.AskAsync(this, document.Title) switch
        {
            UnsavedChoice.Save => await SaveAsync(document),
            UnsavedChoice.Discard => true,
            _ => false,
        };
    }

    private void Select(DocumentTab document)
    {
        _documents.Activate(document);

        if (_tabs.TryGetValue(document, out var tab))
        {
            Documents.SelectedItem = tab;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Documents.SelectedItem is TabItem selected)
        {
            foreach (var (document, tab) in _tabs)
            {
                if (ReferenceEquals(tab, selected))
                {
                    _documents.Activate(document);
                    break;
                }
            }
        }

        UpdateStatus();
    }

    private void OnDocumentChanged(object? sender, ModelChangedEventArgs e) => UpdateStatus();

    private void ApplyTheme(SyntaxTheme theme)
    {
        _theme = theme;

        // The window chrome follows the document rather than the operating system, because the shell
        // offers a theme switch: asking for a dark palette and getting a white tab strip would be a
        // bug the user can see.
        RequestedThemeVariant = theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        Background = theme.Chrome;

        foreach (var tab in _tabs.Values)
        {
            if (tab.Content is ProjectionalEditorControl editor)
            {
                ApplyThemeTo(editor, theme);
            }
        }
    }

    private static void ApplyThemeTo(ProjectionalEditorControl editor, SyntaxTheme theme)
    {
        // Four properties rather than one, because the control refuses to guess what surface its
        // palette is meant for: a dark palette is only readable on a dark background.
        editor.Palette = theme.Palette;
        editor.Background = theme.Background;
        editor.CaretBrush = theme.Caret;
        editor.HighlightBrush = theme.Highlight;
    }

    private void UpdateStatus()
    {
        foreach (var (document, tab) in _tabs)
        {
            tab.Header = document.IsDirty ? $"{document.Title} *" : document.Title;
        }

        var position = _documents.Active is not { } active
            ? "No document open"
            : $"{active.Title}{FormatFile(active.FilePath)} | {(_documents.AnyUnsaved ? "unsaved changes" : "saved")} | {(_theme.IsDark ? "dark" : "light")} theme";

        // The message goes last and is not cleared here: it is the answer to the command the user just
        // ran, and the next command clears it, so it cannot go stale while the user reads it.
        StatusText.Text = _error is null ? position : $"{position} | {_error}";

        UpdateRepresentations();
    }

    /// <summary>Fills the representation submenu from the active document's alternatives.</summary>
    /// <remarks>
    /// <para>
    /// The list comes from <see cref="EditorMenu.Representations"/> rather than from the session
    /// directly: the context menu offers the same choice, and a concept's variants may only be offered
    /// one way. The window contributes only the location.
    /// </para>
    /// <para>
    /// It is rebuilt rather than data-bound because the alternatives depend on the document in front
    /// and on its model: an edit can introduce a concept that declares a second notation, and the menu
    /// has to offer it without the shell having to notice that on its own. It is refreshed with the
    /// status bar, which is the one place that already runs whenever any of those things change.
    /// </para>
    /// </remarks>
    private void UpdateRepresentations()
    {
        var entries = _documents.Active is { } document
            ? EditorMenu.Representations(document.Session)
            : [];

        var items = EditorMenuPresenter.Items(entries, AfterRepresentation);

        // No alternative means nothing to choose from, which is a different thing from a document whose
        // one concept can be drawn a single way: disabling says so instead of offering an empty menu.
        Representations.IsEnabled = items.Count > 0;
        Representations.ItemsSource = items;
    }

    private void AfterRepresentation()
    {
        RefreshActive();

        // Posted, not called: this runs from the click of an item whose menu is still closing, and
        // rebuilding the list now would replace that item underneath the click.
        Dispatcher.UIThread.Post(UpdateStatus);
    }

    private void RefreshActive()
    {
        if (_documents.Active is { } document
            && _tabs.TryGetValue(document, out var tab)
            && tab.Content is ProjectionalEditorControl editor)
        {
            editor.Refresh();
        }
    }

    private static string FormatFile(string? filePath) =>
        filePath is null ? string.Empty : $" ({Path.GetFileName(filePath)})";

    private void FocusActiveDocument()
    {
        if (_documents.Active is { } document && _tabs.TryGetValue(document, out var tab))
        {
            (tab.Content as ProjectionalEditorControl)?.Focus();
        }
    }

    private void OnNewDocument(object? sender, RoutedEventArgs e)
    {
        _error = null;
        OpenDocument(PayrollSample.NewEmptyModel(), RulesLanguage.CreateNotations(RulesLanguage.Concepts));
    }

    private async void OnOpenDocument(object? sender, RoutedEventArgs e)
    {
        _error = null;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a model",
            AllowMultiple = false,
            FileTypeFilter = [ModelFileType()],
        });

        if (files.Count == 0)
        {
            return;
        }

        if (files[0].TryGetLocalPath() is not { } path)
        {
            _error = "That file has no path this app can read.";
            UpdateStatus();

            return;
        }

        if (_documents.Find(path) is { } alreadyOpen)
        {
            Select(alreadyOpen);
            UpdateStatus();

            return;
        }

        if (DocumentFile.TryLoad(path, out var error) is not { } model)
        {
            _error = $"Could not open {Path.GetFileName(path)}: {error}";

            UpdateStatus();

            return;
        }

        OpenDocument(model, RulesLanguage.CreateNotations(model.Concepts), filePath: path, emphasis: RulesLanguage.Emphasis);
    }

    private async void OnSaveDocument(object? sender, RoutedEventArgs e)
    {
        _error = null;

        if (_documents.Active is { } document)
        {
            await SaveAsync(document);
        }
    }

    private async void OnSaveDocumentAs(object? sender, RoutedEventArgs e)
    {
        _error = null;

        if (_documents.Active is { } document && await PickSavePathAsync(document) is { } path)
        {
            SaveTo(document, path);
        }
    }

    /// <summary>Writes <paramref name="document"/> where it came from, asking for a place the first time.</summary>
    /// <param name="document">The document to write.</param>
    /// <returns>Whether the document reached the disk.</returns>
    private async Task<bool> SaveAsync(DocumentTab document)
    {
        if (document.FilePath is { } known)
        {
            return SaveTo(document, known);
        }

        if (await PickSavePathAsync(document) is not { } chosen)
        {
            return false;
        }

        return SaveTo(document, chosen);
    }

    private bool SaveTo(DocumentTab document, string path)
    {
        if (DocumentFile.TrySave(document, path, out var error))
        {
            UpdateStatus();

            return true;
        }

        _error = $"Could not save {Path.GetFileName(path)}: {error}";
        UpdateStatus();

        return false;
    }

    private async Task<string?> PickSavePathAsync(DocumentTab document)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the model",
            SuggestedFileName = document.Title,
            DefaultExtension = DocumentFile.Extension.TrimStart('.'),
            FileTypeChoices = [ModelFileType()],
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType ModelFileType() => new("Projectional model")
    {
        Patterns = [$"*{DocumentFile.Extension}"],
    };

    private void OnCloseDocument(object? sender, RoutedEventArgs e) => CloseActiveDocument();

    private void OnLightTheme(object? sender, RoutedEventArgs e)
    {
        ApplyTheme(SyntaxTheme.Light);
        UpdateStatus();
    }

    private void OnDarkTheme(object? sender, RoutedEventArgs e)
    {
        ApplyTheme(SyntaxTheme.Dark);
        UpdateStatus();
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();
}
