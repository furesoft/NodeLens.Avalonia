using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;

using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Expressions.Intentions;
using Projectional.Intentions;
using Projectional.Language;
using Projectional.LanguageExport;
using Projectional.Languages;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Samples.Intentions;
using Projectional.Semantics;
using Projectional.Structures.Intentions;
using Projectional.Testing;

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
    /// <summary>The size documents are drawn at until the user zooms.</summary>
    private const double DefaultFontSize = 14;

    private const double MinimumFontSize = 6;
    private const double MaximumFontSize = 48;
    private const double FontSizeStep = 1;

    /// <summary>The least a drag may leave of the panel area, so the splitter cannot lose it.</summary>
    private const double MinimumPanelHeight = 96;

    /// <summary>What the window is called when no document decides it, and the half that stays.</summary>
    private const string WindowTitle = "Projectional Workbench";

    private readonly DocumentSet _documents = new();

    /// <summary>What this window may offer; the one statement the menus, the panels and the commands read.</summary>
    private readonly ShellCapability _capabilities;
    private readonly WorkbenchLanguage _language = new();
    private readonly Dictionary<DocumentTab, TabItem> _tabs = [];

    /// <summary>
    /// The one clipboard every tab reads and writes.
    /// </summary>
    /// <remarks>
    /// Shared on purpose: a copied subtree has to be pasteable into another document, and the resource is the
    /// window's platform clipboard rather than something per tab. It is also the adapter the controls refresh,
    /// so a session on a different one would paste text the platform has already replaced.
    /// </remarks>
    private readonly AvaloniaClipboard _clipboard;
    private readonly TextRecentFilesStore _recentStore = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Projectional",
            "recent.txt"));

    private RecentFiles _recent = RecentFiles.Empty;

    private SyntaxTheme _theme = SyntaxTheme.Light;
    private ThemeVariant _themeChoice = ThemeVariant.Default;
    private bool _showLineNumbers;
    private double _fontSize = DefaultFontSize;
    private string? _error;
    private NodeId? _usageTarget;
    private NodeId? _renameTarget;
    private bool _closeConfirmed;
    private bool _disposed;

    // The panel area keeps two things apart: what the content allows and what the user asked for. A panel
    // closed by the user must not come back because the document changed, which is what re-deriving the rows
    // alone would do on every keystroke.
    private readonly TabItem[] _panelOrder;
    private readonly double _splitterHeight;
    private readonly double _sideWidth;
    private double _panelHeight;
    private bool _outputClosed;
    private bool _problemsClosed;
    private bool _usagesClosed;

    /// <summary>Creates the window with every module - the standalone editor.</summary>
    public MainWindow() : this(ShellCapabilities.Standalone)
    {
    }

    /// <summary>Creates the window over <paramref name="capabilities"/>, which is what a host decides.</summary>
    /// <param name="capabilities">The modules and actions this shell may offer.</param>
    /// <remarks>
    /// Internal rather than public because the shell is not a library yet (ADR 0015 puts that extraction at M6);
    /// a host today embeds it through this seam, and the seams the screenshots use are internal for the same
    /// reason. When M6 lands, this parameter moves to <c>ShellView</c> unchanged.
    /// </remarks>
    internal MainWindow(ShellCapability capabilities)
    {
        _capabilities = capabilities;

        InitializeComponent();

        // The area's own measurements rather than a second copy of them: the XAML declares the shape, and
        // the window only has to know what to restore when the last panel leaves and one comes back.
        _panelOrder = [OutputTab, ProblemsTab, UsagesTab];
        _panelHeight = DocumentArea.RowDefinitions[2].Height.Value;
        _splitterHeight = DocumentArea.RowDefinitions[1].Height.Value;
        _sideWidth = SideArea.ColumnDefinitions[0].Width.Value;

        Compose();

        // Before the first tab exists: a session takes its clipboard when it is created, and the sample opens
        // below.
        _clipboard = new AvaloniaClipboard(() => TopLevel.GetTopLevel(this)?.Clipboard);

        // Following the desktop is the choice the window starts with: opening light on a dark system looks
        // broken before the user has done anything.
        ApplyThemeChoice(ThemeVariant.Default);
        OpenSample();

        // Tunnelling, because the editor control would otherwise see the key first and could claim it:
        // zoom is a property of the shell, not of one document.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        Documents.SelectionChanged += OnSelectionChanged;
        Opened += (_, _) => FocusActiveDocument();

        // A file dragged in from the desktop is the shortest way to open it. The window is the target, so the
        // gesture works wherever the pointer happens to be - over a panel, the outline or the page.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Read once at startup: a list read per opening would be a list the menu and the shell could disagree about,
        // and a broken file must cost one empty menu rather than an exception on every open.
        _recent = RecentFiles.From(_recentStore.Read());
        RebuildRecentMenu();
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

    /// <summary>Composes the window from the capability set: a module it does not grant is not built at all.</summary>
    /// <remarks>
    /// <para>
    /// Removing a module where it is *composition* - a menu, a tab, a whole column - and refusing it where it is a
    /// *command*: both halves are needed, because a hidden entry is presentation and a refusal is the guarantee
    /// (ADR 0015). This is the one place that reads the set for composition, so a host's shell cannot end up with
    /// half of a module.
    /// </para>
    /// <para>
    /// The side column and the panel area are taken apart by the same code that hides them when they are empty, so a
    /// shell without the project explorer gets its width back rather than an empty strip.
    /// </para>
    /// </remarks>
    private void Compose()
    {
        FileMenu.IsVisible = true;
        RunMenu.IsVisible = _capabilities.Allows(ShellCapability.Run) || _capabilities.Allows(ShellCapability.Tests);

        OpenDocumentItem.IsVisible = _capabilities.Allows(ShellCapability.OpenFiles);
        OpenLanguageItem.IsVisible = _capabilities.Allows(ShellCapability.OpenFiles);
        NewDocumentItem.IsVisible = _capabilities.Allows(ShellCapability.CreateModels);
        ImportDocumentItem.IsVisible = _capabilities.Allows(ShellCapability.Import);
        ExportLanguageItem.IsVisible = _capabilities.Allows(ShellCapability.Export);
        RecentFilesItem.IsVisible = _capabilities.Allows(ShellCapability.OpenFiles);
        SaveDocumentItem.IsVisible = _capabilities.Allows(ShellCapability.Save);
        SaveAsItem.IsVisible = _capabilities.Allows(ShellCapability.Save);
        SaveGeneratedItem.IsVisible = _capabilities.Allows(ShellCapability.Save);

        EditMenu.IsVisible = _capabilities.Allows(ShellCapability.EditModels);
        FindItem.IsVisible = _capabilities.Allows(ShellCapability.Find);
        ReplaceItem.IsVisible = _capabilities.Allows(ShellCapability.Find);
        FindUsagesItem.IsVisible = _capabilities.Allows(ShellCapability.FindUsages);
        ViewFindItem.IsVisible = _capabilities.Allows(ShellCapability.Find);
        ViewFindUsagesItem.IsVisible = _capabilities.Allows(ShellCapability.FindUsages);

        RunDocumentItem.IsVisible = _capabilities.Allows(ShellCapability.Run);
        RunTestsItem.IsVisible = _capabilities.Allows(ShellCapability.Tests);
        ClearOutputItem.IsVisible = _capabilities.Allows(ShellCapability.Run);
        GenerateItem.IsVisible = _capabilities.Allows(ShellCapability.Run);

        // The side column: the tabs a host did not ask for are not in it, and a column nobody wants is not there.
        ProjectTab.IsVisible = _capabilities.Allows(ShellCapability.ProjectExplorer);
        OutlineTab.IsVisible = _capabilities.Allows(ShellCapability.Outline);
        OutlinePanel.IsVisible = ProjectTab.IsVisible || OutlineTab.IsVisible;
        SideSplitter.IsVisible = OutlinePanel.IsVisible;
        SideArea.ColumnDefinitions[0].Width = new GridLength(OutlinePanel.IsVisible ? _sideWidth : 0);
        SideArea.ColumnDefinitions[1].Width = new GridLength(OutlinePanel.IsVisible ? _splitterHeight : 0);

        // The panels, the same way: no problems module, no problems tab and no count.
        _problemsClosed = !_capabilities.Allows(ShellCapability.Problems);
        _outputClosed = !_capabilities.Allows(ShellCapability.Run);
        _usagesClosed = !_capabilities.Allows(ShellCapability.FindUsages);

        EmptyState.IsVisible = _documents.Active is null;
    }

    /// <summary>Whether this shell may offer <paramref name="capability"/>.</summary>
    private bool Allows(ShellCapability capability) => _capabilities.Allows(capability);

    /// <summary>The document in front is editable, or the shell was declared read-only.</summary>
    private bool CanEdit => Allows(ShellCapability.EditModels);

    private void OpenSample()
    {
        var model = PayrollSample.NewSampleModel();

        OpenDocument(model, _language.Notations, emphasis: WorkbenchLanguage.Emphasis, typing: WorkbenchLanguage.Typing);
    }

    /// <summary>Opens <paramref name="model"/> in a new tab, or brings its tab forward when it is already open.</summary>
    /// <remarks>
    /// Internal rather than private because the screenshot tool drives the shell through this seam: a picture of a
    /// shell feature needs a document the shell's own sample does not hold, and a file picker cannot be driven from a
    /// test harness. It is not public because the shell is a library only once M6 extracts it (ADR 0015); until then,
    /// the app is the only place a caller of this method may live.
    /// </remarks>
    internal void OpenDocument(
        Model model,
        NotationRegistry notations,
        string? filePath = null,
        ICellEmphasis? emphasis = null,
        ITypingRules? typing = null,
        TypeRegistry? types = null,
        ConstraintSet? constraints = null,
        IScopeProvider? scopes = null,
        IIntentionProvider? intentions = null,
        IReadOnlyList<TextGenerator>? generators = null,
        Projectional.Checks.CheckSet? checks = null)
    {
        if (_documents.Find(model) is { } alreadyOpen)
        {
            Select(alreadyOpen);

            return;
        }

        var editor = new ProjectionalEditorControl();

        // Before the tab is built, not after: the session takes its measure from the control, so a size
        // applied afterwards would have to rebuild a layout that was already laid out at 14.
        editor.FontSize = _fontSize;
        editor.Clipboard = _clipboard;

        // The interpreter is derived from the model rather than passed in, because "can this document run" is a fact
        // about the language it is written in - a caller cannot forget it and cannot hand a document someone else's
        // semantics either.
        var document = new DocumentTab(
            model,
            notations,
            editor.TextMeasure,
            filePath,
            emphasis,
            typing,
            types,
            constraints,
            scopes,
            intentions,
            WorkbenchLanguage.InterpreterFor(model),
            generators ?? WorkbenchLanguage.GeneratorsFor(model),
            checks,
            _clipboard);
        editor.Session = document.Session;
        ApplyThemeTo(editor, _theme);
        editor.ShowLineNumbers = _showLineNumbers;

        // Once per document, not per change: a provider is a source, not a snapshot, and registering it
        // again on every edit would list its entries twice.
        RegisterIntentions(document);
        PublishFindings(document, editor);

        // The header has to say "modified" without the user saving to find out, so the model itself
        // is the notification: an edit anywhere in it changes the dirty state.
        model.Changed += OnDocumentChanged;

        // The status bar says where the caret is, so the control has to say when it moved.
        editor.SelectionChanged += OnCaretMoved;

        var tab = new TabItem { Content = ScrollSurface(editor) };

        // A tab carries its own close button and dirty mark: the gesture alone is not a control.
        tab.Header = new DocumentTabHeader(document.Title, () => CloseFromTab(document));

        _documents.Add(document);
        _tabs.Add(document, tab);

        Documents.Items.Add(tab);
        Documents.SelectedItem = tab;
        tab.BringIntoView();

        ShowProblems();
        ShowOutput();
        ShowOutline();
        ShowProject();
        SyncUsages();
        SyncRename();
        UpdateStatus();
    }

    /// <summary>Puts <paramref name="editor"/> behind platform scroll bars.</summary>
    /// <remarks>
    /// <para>
    /// The editor implements the logical scroll protocol and owns no bar of its own, so this is the one
    /// place the app decides that a document is scrollable and how a bar looks: the bar is drawn by the
    /// toolkit, in the host's theme, which is right - a bar in the document's palette would be chrome
    /// pretending to be content.
    /// </para>
    /// <para>
    /// <c>Focusable = false</c> on the wrapper because the keyboard belongs to the editor. A focusable
    /// <c>ScrollViewer</c> takes the Tab key for itself, which would stop the caret moving between slots,
    /// and the editor already answers every scrolling gesture a bar could offer.
    /// </para>
    /// </remarks>
    private static ScrollViewer ScrollSurface(ProjectionalEditorControl editor) => new()
    {
        Content = editor,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Focusable = false,
    };

    /// <summary>The editor inside <paramref name="tab"/>, or nothing when the tab holds something else.</summary>
    /// <remarks>
    /// A tab's content is the scroll wrapper, not the editor, so every one of the six places that need the
    /// editor goes through this rather than repeating the cast - a second shape of wrapper would otherwise
    /// have to be found and understood in each of them.
    /// </remarks>
    private static ProjectionalEditorControl? EditorOf(TabItem tab) =>
        tab.Content is ScrollViewer { Content: ProjectionalEditorControl editor } ? editor : null;

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

    /// <summary>Closes the document a tab's own close button belongs to, asking about unsaved changes first.</summary>
    /// <param name="document">The document whose tab was clicked shut.</param>
    private async void CloseFromTab(DocumentTab document)
    {
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
            if (EditorOf(tab) is { } editor)
            {
                editor.SelectionChanged -= OnCaretMoved;
            }

            Documents.Items.Remove(tab);
        }

        if (_documents.Active is { } next)
        {
            Select(next);
        }

        ShowProblems();
        ShowOutput();
        ShowOutline();
        ShowProject();
        SyncUsages();
        SyncRename();
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

            // The strip scrolls now, so a tab that was off the right edge has to be scrolled to.
            tab.BringIntoView();
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

        ShowProblems();
        ShowOutput();
        ShowOutline();
        ShowProject();
        SyncUsages();
        SyncRename();
        UpdateStatus();
    }

    private void OnDocumentChanged(object? sender, ModelChangedEventArgs e)
    {
        // Findings are a function of the model, so the model is the notification. A host that had to
        // remember to re-run its checks would leave the squiggles describing the document as it was
        // before the edit - which is the one moment they are certainly wrong.
        if (sender is Model model
            && _documents.Find(model) is { } document
            && _tabs.TryGetValue(document, out var tab)
            && EditorOf(tab) is { } editor)
        {
            PublishFindings(document, editor);

            // A run is a snapshot of the document as it was, so an edit drops it. Findings can be derived again
            // from the new model; a run cannot, because the variables came from the user. A test run is a snapshot
            // of the same kind: it was true of the document before the edit.
            document.Output = null;
            document.Generated = null;
            document.Tests = null;
        }

        ShowProblems();
        ShowOutput();
        ShowOutline();
        ShowProject();
        SyncUsages();
        SyncRename();
        UpdateStatus();
    }

    /// <summary>Hands the editor what the checks found in a document, and what repairs they offer.</summary>
    /// <remarks>
    /// <para>
    /// Two checks, both of them the app's own: the shell's placeholder names and the sample language's
    /// duplicated rule names. Both answer with fixes, and a fix carries the finding it repairs, so the
    /// marker set is derived from the repairs rather than collected a second time - the two sets cannot
    /// describe different models because they are the same list.
    /// </para>
    /// <para>
    /// The editor resolves nothing: it draws the diagnostics it is given and offers the repairs it is
    /// given, so the shell decides what to report and a host that wants different checks replaces this
    /// call with its own.
    /// </para>
    /// </remarks>
    private static void PublishFindings(DocumentTab document, ProjectionalEditorControl editor)
    {
        // A document whose language brings checks or constraints of its own is checked with them, and with nothing
        // else: the shell's two rules are about names, and a language that declares findings of its own has said what
        // it wants a name to be. A declared language has no check set - what it states are constraints - so asking
        // only for `Checks` made the pair run beside them, and a stage of the pipeline sample reported the same hint
        // twice: once from SHELL001 and once from the language's own rule. A document that brings neither keeps the
        // shell's pair, so every document the app has always opened is checked exactly as before.
        List<DiagnosticFix> fixes =
            document.Checks is { } checks ? [.. checks.Find(document.Model)] :
            document.Constraints is not null ? [] :
            [.. DefaultNames.Of(document.Model), .. DuplicateRuleNames.Of(document.Model)];

        // The join itself lives beside the control, because a host owes its users the same one list. The
        // session's scope provider, not the default one: a language may have replaced a level of the chain,
        // and a finding about a name has to be about the scope the editor actually uses. A control with no
        // session yet passes null, which the engine reads as the default chain. The rules are the document's
        // own when its language declares any - the sample's when it does not, which keeps every document the
        // shell has always opened checked exactly as before.
        var findings = DocumentFindings.Of(
            document.Model,
            fixes,
            document.Constraints ?? SampleConstraints.All,
            document.Types ?? ExpressionsLanguage.Types,
            editor.Session?.Scopes,
            GenerationCoverage.Of(document.Model, document.Generators));

        editor.Markers = findings.Markers;
        editor.Fixes = findings.Fixes;

        // The panel reads the same list the squiggles are drawn from, so the two cannot disagree about
        // what was found - one of them would otherwise have to run the checks again.
        document.Problems = ProblemList.Of(document.Model, findings.Diagnostics);
    }

    /// <summary>Undoes one step of the document in front.</summary>
    private void OnUndo(object? sender, RoutedEventArgs e) => RunShellEdit(session => session.Undo());

    /// <summary>Redoes one step of the document in front.</summary>
    private void OnRedo(object? sender, RoutedEventArgs e) => RunShellEdit(session => session.Redo());

    private void OnCut(object? sender, RoutedEventArgs e) =>
        RunShellEdit(session => session.Apply(new CutAction()).Changed);

    private void OnCopy(object? sender, RoutedEventArgs e) =>
        RunShellEdit(session => session.Apply(new CopyAction()).Changed);

    private void OnSelectAll(object? sender, RoutedEventArgs e) =>
        RunShellEdit(session => session.Apply(new SelectAllAction()).Changed);

    /// <summary>Pastes what the platform clipboard holds into the document in front.</summary>
    /// <remarks>
    /// The clipboard is read first: another application may have filled it since this one last wrote, and
    /// the session's cache would otherwise paste what the user no longer has.
    /// </remarks>
    private async void OnPaste(object? sender, RoutedEventArgs e)
    {
        if (ActiveEditor() is not { } editor || editor.Session is not { } session)
        {
            return;
        }

        await _clipboard.RefreshAsync();

        if (session.Apply(new PasteAction()).Outcome == EditOutcome.Rejected)
        {
            _error = "The clipboard's text does not fit the place the caret is in.";
            UpdateStatus();
        }

        editor.Refresh();
        editor.ScrollToCaret();
    }

    /// <summary>Runs one editing command against the document in front and repaints it.</summary>
    /// <remarks>
    /// An undo, a copy and a selection move change nothing the control can observe on its own - none of them is
    /// a model change - so the host repaints, the same rule the representation menu follows.
    /// </remarks>
    private void RunShellEdit(Func<EditorSession, bool> edit)
    {
        if (ActiveEditor() is not { } editor || editor.Session is not { } session)
        {
            return;
        }

        edit(session);

        editor.Refresh();
        editor.ScrollToCaret();
    }

    /// <summary>Opens the find bar and puts the caret in it.</summary>
    private void OnFindText(object? sender, RoutedEventArgs e) => OpenFindBar(withReplace: false);

    /// <summary>Opens the find bar with the replace row, for a user who came to replace.</summary>
    private void OnFindReplace(object? sender, RoutedEventArgs e) => OpenFindBar(withReplace: true);

    private void OpenFindBar(bool withReplace)
    {
        if (!Allows(ShellCapability.Find))
        {
            return;
        }

        FindBar.IsVisible = true;
        ReplaceToggle.IsChecked = withReplace;
        ReplaceRow.IsVisible = withReplace;

        if (withReplace)
        {
            ReplaceInput.Focus();
            ReplaceInput.SelectAll();
        }
        else
        {
            FindInput.Focus();
            FindInput.SelectAll();
        }

        UpdateFindCount();
    }

    /// <summary>Shows the replace row while the toggle is on, and hides it again when it is off.</summary>
    private void OnReplaceToggle(object? sender, RoutedEventArgs e) =>
        ReplaceRow.IsVisible = ReplaceToggle.IsChecked == true;

    /// <summary>Re-counts the matches as the query changes.</summary>
    /// <remarks>
    /// The count is asked of <see cref="Finder"/> rather than kept from the last search, because the query and the
    /// document both change: a number that outlived either would be a number nobody can act on.
    /// </remarks>
    private void OnFindQueryChanged(object? sender, RoutedEventArgs e) => UpdateFindCount();

    /// <summary>Enter searches forward, Shift+Enter backward, Escape leaves the bar.</summary>
    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                FindStep(FindDirection.Backward);
                break;

            case Key.Enter:
                FindStep(FindDirection.Forward);
                break;

            case Key.Escape:
                CloseFind();
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private void OnFindNext(object? sender, RoutedEventArgs e) => FindStep(FindDirection.Forward);

    private void OnFindPrevious(object? sender, RoutedEventArgs e) => FindStep(FindDirection.Backward);

    /// <summary>Replaces the selected occurrence, or finds the next one when nothing is selected.</summary>
    /// <remarks>
    /// A button that is dead until the user has searched would be a button they cannot use to start: so the press
    /// either replaces what is selected - when that is exactly an occurrence - or moves to the next occurrence, which
    /// is also what the user wants when the first press came before the first search.
    /// </remarks>
    private void OnReplace(object? sender, RoutedEventArgs e)
    {
        if (Query is not { } query
            || _documents.Active is not { } document
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        var replacement = ReplaceInput.Text ?? string.Empty;
        var matches = Finder.Find(document.Session, query);
        var selected = document.Session.Selection;

        foreach (var match in matches)
        {
            if (!match.Address.Equals(selected.Caret.Address)
                || match.Offset != selected.Start
                || match.Length != selected.End - selected.Start)
            {
                continue;
            }

            document.Session.Apply(new ReplaceMatchAction(match, replacement));
            break;
        }

        FindStep(FindDirection.Forward);

        editor.Refresh();
    }

    /// <summary>Replaces every occurrence of the query, as one undo step.</summary>
    private void OnReplaceAll(object? sender, RoutedEventArgs e)
    {
        if (Query is not { } query
            || _documents.Active is not { } document
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        document.Session.Apply(new ReplaceAllAction(query, ReplaceInput.Text ?? string.Empty));

        editor.Refresh();
        editor.ScrollToCaret();

        UpdateFindCount();
    }

    /// <summary>Selects the next occurrence and tells the user where it is.</summary>
    private void FindStep(FindDirection direction)
    {
        if (Query is not { } query
            || _documents.Active is not { } document
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        document.Session.Apply(new FindNextAction(query, direction));

        // The session changes nothing the control can observe, so the host repaints - the same rule the
        // representation menu follows.
        editor.Refresh();
        editor.ScrollToCaret();

        UpdateFindCount();
    }

    /// <summary>Fills in the match count for the active document.</summary>
    private void UpdateFindCount()
    {
        if (Query is not { } query)
        {
            FindCount.Text = string.Empty;
            return;
        }

        var matches = _documents.Active is { } document ? Finder.Find(document.Session, query) : [];

        FindCount.Text = matches.Count switch
        {
            0 => "no matches",
            1 => "1 match",
            _ => $"{matches.Count} matches",
        };
    }

    /// <summary>Hides the bar and gives the keyboard back to the document.</summary>
    private void CloseFind()
    {
        FindBar.IsVisible = false;
        FocusActiveDocument();
    }

    /// <summary>The query the bar describes, or <see langword="null"/> while it holds no text.</summary>
    private FindQuery? Query =>
        FindInput.Text is { Length: > 0 } text ? new FindQuery(text, FindCase.IsChecked == true) : null;

    /// <summary>Opens the rename bar for the node the caret names.</summary>
    /// <remarks>
    /// The target is the same node find usages asks about: a caret on a reference names what it points at, a caret
    /// anywhere else names the node whose cell it is in. A node of another model is refused because this document's
    /// undo history is what would have to take the rename back.
    /// </remarks>
    private void OnRename(object? sender, RoutedEventArgs e) => OpenRenameBar();

    private void OpenRenameBar()
    {
        if (_documents.Active is not { } document)
        {
            return;
        }

        var target = FindUsages.TargetOf(document.Session);

        if (target is null)
        {
            _error = "Nothing to rename: the caret is not on a node or a resolved reference.";
            UpdateStatus();

            return;
        }

        if (!ReferenceEquals(target.Model, document.Model))
        {
            _error = "That node belongs to another document; open it to rename it.";
            UpdateStatus();

            return;
        }

        if (target.Concept.NameSlot is not { } nameSlot)
        {
            _error = $"'{target.Concept.Key}' declares no name slot, so it cannot be renamed.";
            UpdateStatus();

            return;
        }

        _error = null;
        _renameTarget = target.Id;
        RenameBar.IsVisible = true;
        RenameInput.Text = target.GetOrNull(nameSlot.Name) ?? string.Empty;
        RenameInput.Focus();
        RenameInput.SelectAll();
        UpdateStatus();
    }

    /// <summary>Enter renames, Escape cancels.</summary>
    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ApplyRename();
                break;

            case Key.Escape:
                CloseRename();
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private void OnRenameApply(object? sender, RoutedEventArgs e) => ApplyRename();

    private void OnRenameCancel(object? sender, RoutedEventArgs e) => CloseRename();

    /// <summary>Applies the typed name as one edit and closes the bar.</summary>
    /// <remarks>
    /// A refused rename leaves the bar open with the reason in the status bar: the value came from the user, and
    /// correcting it is one keystroke away. Everything the action touches - the name and every fallback of this
    /// model - is one undo step.
    /// </remarks>
    private void ApplyRename()
    {
        if (_documents.Active is not { } document || _renameTarget is not { } id)
        {
            return;
        }

        var result = document.Session.Apply(new RenameNodeAction(id, RenameInput.Text ?? string.Empty));

        if (result.Outcome == EditOutcome.Rejected)
        {
            _error = result.Reason;
            UpdateStatus();

            return;
        }

        _error = null;
        CloseRename();

        if (_tabs.TryGetValue(document, out var tab) && EditorOf(tab) is { } editor)
        {
            editor.Refresh();
            editor.ScrollToCaret();
        }

        UpdateStatus();
    }

    /// <summary>Closes the bar and gives the keyboard back to the document.</summary>
    private void CloseRename()
    {
        HideRename();
        FocusActiveDocument();
    }

    /// <summary>Closes the bar without moving the focus, as a tab switch needs.</summary>
    private void HideRename()
    {
        _renameTarget = null;
        RenameBar.IsVisible = false;
    }

    /// <summary>Closes a bar whose target left the document or the document in front.</summary>
    private void SyncRename()
    {
        if (!RenameBar.IsVisible)
        {
            return;
        }

        if (_renameTarget is not { } id
            || _documents.Active is not { } document
            || document.Model.Project.FindNode(id) is null)
        {
            HideRename();
        }
    }

    /// <summary>Fills the project explorer from the open documents and the opened languages.</summary>
    /// <remarks>
    /// Rebuilt wherever the outline is, and for the same reason: it describes what is open, so a stale list would
    /// name a document that was closed. The language half is read from disk per build, which is a handful of small
    /// folders - a language is a few dozen documents, not a solution.
    /// </remarks>
    private void ShowProject()
    {
        var rows = ProjectExplorer.Of(_documents.Documents, _language.Opened);

        ProjectList.ItemsSource = rows;

        // The row of the document in front is selected, which is how the panel says which one it is - the tab
        // strip's own selection is one place, and the two must not be able to disagree.
        ProjectList.SelectedItem = rows.FirstOrDefault(row => ReferenceEquals(row.Document, _documents.Active));
    }

    /// <summary>Opens or activates what a project row stands for.</summary>
    private void OnProjectChosen(object? sender, RoutedEventArgs e)
    {
        if (ProjectList.SelectedItem is not ProjectEntry entry)
        {
            return;
        }

        if (entry.Document is { } document)
        {
            Select(document);

            return;
        }

        if (entry.Path is { } path)
        {
            OpenProjectFile(path);
        }
    }

    private void OnProjectKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        OnProjectChosen(sender, e);
    }

    /// <summary>Opens a file of an opened language, the way the language itself opens its definitions.</summary>
    /// <param name="path">The file the row named.</param>
    /// <remarks>
    /// A definition document is drawn with the shell's combined notations rather than with the rules language's
    /// alone - the same registries <see cref="OpenLanguage"/> uses - because a concept file borrows its notation from
    /// the editor aspects it declares.
    /// </remarks>
    private void OpenProjectFile(string path)
    {
        if (_documents.Find(path) is { } open)
        {
            Select(open);

            return;
        }

        _error = null;

        if (DocumentFile.TryLoad(path, _language.NewProject(), out var error) is not { } model)
        {
            _error = $"Could not open {Path.GetFileName(path)}: {error}";
            UpdateStatus();

            return;
        }

        RememberRecent(path);
        OpenDocument(model, _language.Notations, path, WorkbenchLanguage.Emphasis, WorkbenchLanguage.Typing);
    }

    /// <summary>Fills the model browser from the active document.</summary>
    /// <remarks>
    /// Rebuilt on every model change, like the findings: the outline describes the document, so a stale one would list
    /// nodes that were renamed or deleted. The cost is a walk of the active model per change, which is the same order
    /// as the checks that already run there - a browser that had to be refreshed by a command would be one the user
    /// forgets to refresh.
    /// </remarks>
    private void ShowOutline()
    {
        OutlineList.ItemsSource = _documents.Active is { } document ? Outline.Of(document.Model) : [];
        OutlineList.SelectedItem = null;

        // The header names the document the rows belong to: a panel called "Models" that lists one document's
        // nodes is a header describing something nobody is looking at.
        OutlineHeader.Text = _documents.Active is { } active ? $"Outline - {active.Title}" : "Outline";
    }

    /// <summary>Puts the caret on the node a browser row stands for.</summary>
    /// <remarks>
    /// A row names a node, so the address is the node's own: the caret resolves it to the first place that node draws,
    /// which is the same place a node selection starts at.
    /// </remarks>
    private void OnOutlineChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (OutlineList.SelectedItem is not OutlineEntry entry
            || _documents.Active is not { } document
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        document.Session.Select(Selection.At(new CellAddress(entry.Id)));
        editor.Refresh();
        editor.ScrollToCaret();
        editor.Focus();
    }

    /// <summary>Fills the problems tab from the active document, and shows it while there is something to report.</summary>
    private void ShowProblems()
    {
        var rows = _documents.Active?.Problems ?? [];

        Problems.ItemsSource = rows;
        Problems.SelectedItem = null;
        ProblemsHeader.Text = $"Problems ({rows.Count})";
        ShowPanel(ProblemsTab, rows.Count > 0 && !_problemsClosed);
    }

    /// <summary>Fills the output tab from the active document's last run, and shows it while there is one.</summary>
    /// <remarks>
    /// The headline is the answer, so it goes in the header rather than in a row: a first row that says
    /// <c>Accept</c> would read as one more step of the trace, and the trace is exactly what must stay
    /// comparable across runs.
    /// </remarks>
    private void ShowOutput()
    {
        var report = _documents.Active?.Output;

        RunOutput.ItemsSource = report?.Rows;
        RunOutput.SelectedItem = null;
        OutputHeader.Text = report is null ? "Output" : $"Output - {report.Headline}";
        ShowPanel(OutputTab, report is not null && !_outputClosed);
    }

    /// <summary>Puts the caret on what a chosen row is about.</summary>
    /// <remarks>
    /// The caret is the app's one answer to "show me this": selecting the place scrolls it into view, focuses the
    /// editor and leaves the user where the row pointed - so the next keystroke, or Alt+Enter for the fix, acts on the
    /// finding rather than on wherever the caret happened to be.
    /// </remarks>
    private void OnProblemChosen(object? sender, RoutedEventArgs e)
    {        if (Problems.SelectedItem is not ProblemEntry entry
            || _documents.Active is not { } document
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        // A row is a snapshot, so it can name a node the user has since deleted: say so rather than selecting an
        // address that draws nothing, which looks like the click being ignored.
        if (PlaceResolver.Resolve(document.Session, entry.Place) is not { } place)
        {
            _error = "That problem's node is no longer in the document.";
            UpdateStatus();

            return;
        }

        document.Session.Select(Selection.At(place));
        editor.ScrollToCaret();
        editor.Focus();
    }

    /// <summary>Finds what points at the node the caret names, or at the reference it stands on.</summary>
    private void OnFindUsages(object? sender, RoutedEventArgs e) => ShowActiveUsages();

    private void ShowActiveUsages()
    {
        if (!Allows(ShellCapability.FindUsages))
        {
            return;
        }

        if (_documents.Active is not { } document)
        {
            return;
        }

        _error = null;

        var report = FindUsages.ReportOf(document.Session);

        if (report.Target is null)
        {
            _error = "Nothing to find usages of: the caret is not on a node or a resolved reference.";
            HideUsages();
            UpdateStatus();

            return;
        }

        // Asking again is what brings a tab the user closed back: the gesture is the question.
        _usagesClosed = false;
        ShowUsages(report);
        PanelTabs.SelectedItem = UsagesTab;
        UpdateStatus();
    }

    /// <summary>Fills the usages tab from a report and opens it.</summary>
    private void ShowUsages(UsageReport report)
    {
        if (report.Target is not { } target)
        {
            HideUsages();

            return;
        }

        _usageTarget = target.Id;

        var rows = UsageList.Of(report);

        Usages.ItemsSource = rows;
        Usages.SelectedItem = null;
        UsagesHeader.Text = $"Usages of {UsageList.LabelOf(target)} ({rows.Count})";
        ShowPanel(UsagesTab, true);
    }

    /// <summary>Closes the tab and forgets which node it was answering about.</summary>
    private void HideUsages()
    {
        _usageTarget = null;
        Usages.ItemsSource = null;
        Usages.SelectedItem = null;
        ShowPanel(UsagesTab, false);
    }

    /// <summary>
    /// Closes the usages tab without forgetting the question, so View ▸ Panels can bring the answer back.
    /// </summary>
    private void OnCloseUsages(object? sender, RoutedEventArgs e)
    {
        _usagesClosed = true;
        ShowPanel(UsagesTab, false);
        UpdateStatus();
    }

    /// <summary>Closes the output tab; the next run is what brings it back.</summary>
    private void OnCloseOutput(object? sender, RoutedEventArgs e)
    {
        _outputClosed = true;
        ShowOutput();
        UpdateStatus();
    }

    /// <summary>Closes the problems tab; View ▸ Panels is what brings it back.</summary>
    private void OnCloseProblems(object? sender, RoutedEventArgs e)
    {
        _problemsClosed = true;
        ShowProblems();
        UpdateStatus();
    }

    private void OnToggleOutputPanel(object? sender, RoutedEventArgs e)
    {
        _outputClosed = !_outputClosed;
        ShowOutput();
        UpdateStatus();
    }

    private void OnToggleProblemsPanel(object? sender, RoutedEventArgs e)
    {
        _problemsClosed = !_problemsClosed;
        ShowProblems();
        UpdateStatus();
    }

    /// <summary>Brings the problems tab back from the status bar's count.</summary>
    private void OnShowProblemsPanel(object? sender, RoutedEventArgs e)
    {
        _problemsClosed = false;
        ShowProblems();
        PanelTabs.SelectedItem = ProblemsTab;
        UpdateStatus();
    }

    /// <summary>Enter on a chosen row does what a double click does, so a list is usable without a pointer.</summary>
    private void OnProblemKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        OnProblemChosen(sender, e);
    }

    private void OnUsageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        OnUsageChosen(sender, e);
    }

    private void OnRunRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        OnRunRowChosen(sender, e);
    }

    private void OnToggleUsagesPanel(object? sender, RoutedEventArgs e)
    {
        _usagesClosed = !_usagesClosed;

        if (_usagesClosed)
        {
            ShowPanel(UsagesTab, false);
        }
        else
        {
            SyncUsages();
        }

        UpdateStatus();
    }

    /// <summary>Re-asks the open question, or closes the tab when it can no longer be answered.</summary>
    /// <remarks>
    /// The node id is what keeps the question the one the user asked: re-deriving from the caret would silently turn
    /// a keystroke somewhere else into a new search. The tab closes when its target is gone or belongs to a
    /// document that is no longer in front, because a list of usages of a node the user can no longer see is a list
    /// they cannot act on - and a question the user closed stays closed until it is asked again.
    /// </remarks>
    private void SyncUsages()
    {
        if (_usageTarget is not { } id)
        {
            // No question, no tab: the panel is only there while there is an answer to show.
            if (IsPanelOpen(UsagesTab))
            {
                ShowPanel(UsagesTab, false);
            }

            return;
        }

        if (_documents.Active is not { } document || document.Model.Project.FindNode(id) is not { } target)
        {
            HideUsages();

            return;
        }

        if (_usagesClosed)
        {
            return;
        }

        ShowUsages(FindUsages.Of(target));
    }

    /// <summary>Shows or hides one panel tab, and takes the area away when the last one leaves.</summary>
    /// <param name="tab">The panel's tab.</param>
    /// <param name="visible">Whether it belongs in the area right now.</param>
    private void ShowPanel(TabItem tab, bool visible)
    {
        var open = IsPanelOpen(tab);

        if (visible && !open)
        {
            PanelTabs.Items.Insert(PanelIndex(tab), tab);
        }
        else if (!visible && open)
        {
            PanelTabs.Items.Remove(tab);
        }

        UpdatePanelArea();
    }

    /// <summary>Where <paramref name="tab"/> belongs among the panel tabs that are open.</summary>
    /// <param name="tab">The tab to place.</param>
    /// <remarks>
    /// The order is the declared one rather than the order the tabs happened to be opened in, so a run that
    /// produces a second panel does not put Output behind Problems because Problems came first.
    /// </remarks>
    private int PanelIndex(TabItem tab)
    {
        var index = 0;

        foreach (var candidate in _panelOrder)
        {
            if (ReferenceEquals(candidate, tab))
            {
                break;
            }

            if (IsPanelOpen(candidate))
            {
                index++;
            }
        }

        return index;
    }

    /// <summary>Whether <paramref name="tab"/> is in the panel area.</summary>
    private bool IsPanelOpen(TabItem tab) => PanelTabs.Items.Contains(tab);

    /// <summary>Shows the output tab with the answer a command just produced, and brings it forward.</summary>
    /// <remarks>
    /// A run is a question the user asked, so the answer is shown even if the tab was closed earlier - unlike a
    /// re-derived problem list, which must not reopen a panel the user closed.
    /// </remarks>
    private void RevealOutput()
    {
        _outputClosed = false;
        ShowOutput();
        PanelTabs.SelectedItem = OutputTab;
    }

    /// <summary>Gives the panel area the height it had, or takes it away when nothing is left to show.</summary>
    private void UpdatePanelArea()
    {
        var open = PanelTabs.Items.Count > 0;
        var panel = DocumentArea.RowDefinitions[2];

        if (open)
        {
            panel.MinHeight = MinimumPanelHeight;

            if (panel.Height.Value == 0)
            {
                panel.Height = new GridLength(_panelHeight);
            }
        }
        else
        {
            // Remembered rather than lost, so reopening the area gives back the height the user dragged out.
            // Before the first layout there is no height to remember, and the declared one stays.
            if (PanelArea.Bounds.Height > 0)
            {
                _panelHeight = Math.Max(PanelArea.Bounds.Height, MinimumPanelHeight);
            }

            panel.MinHeight = 0;
            panel.Height = new GridLength(0);
        }

        DocumentArea.RowDefinitions[1].Height = new GridLength(open ? _splitterHeight : 0);

        PanelSplitter.IsVisible = open;
        PanelArea.IsVisible = open;

        if (PanelTabs.SelectedItem is not TabItem selected || !IsPanelOpen(selected))
        {
            PanelTabs.SelectedItem = open ? PanelTabs.Items[0] : null;
        }
    }

    /// <summary>Puts the caret on a chosen usage, opening the document that holds it first.</summary>
    /// <remarks>
    /// A reference the notation hides has no cell, so the row falls back to the owner's node: the nearest place a
    /// reader can be sent to, rather than a press that does nothing. A row whose node has been deleted says so, because
    /// the list is a snapshot and the model is not - both halves are <see cref="PlaceResolver"/>'s now, since the
    /// problems panel asks the same question of the same kind of address.
    /// </remarks>
    private void OnUsageChosen(object? sender, RoutedEventArgs e)
    {
        if (Usages.SelectedItem is not UsageEntry entry)
        {
            return;
        }

        if (_documents.Find(entry.Model) is not { } document)
        {
            _error = "That usage is in a document that is not open.";
            UpdateStatus();

            return;
        }

        _error = null;
        Select(document);

        if (PlaceResolver.Resolve(document.Session, entry.Place) is not { } place)
        {
            _error = "That usage's owner is no longer in the document.";
            UpdateStatus();

            return;
        }

        document.Session.Select(Selection.At(place));

        if (_tabs.TryGetValue(document, out var tab) && EditorOf(tab) is { } editor)
        {
            editor.ScrollToCaret();
            editor.Focus();
        }

        UpdateStatus();
    }

    /// <summary>Runs the document in front and shows what came out.</summary>
    /// <remarks>
    /// The variables are read from the panel's line at the moment of the run, so a keystroke in that box is not a
    /// command: a user who is half way through typing <c>amount=1500</c> must not see a failure about <c>amoun</c>.
    /// </remarks>
    private void OnRunDocument(object? sender, RoutedEventArgs e) => RunActiveDocument();

    private void OnRunTests(object? sender, RoutedEventArgs e) => RunActiveTests();

    /// <summary>Runs the tests folder of the active document's language and shows the rows in the panel.</summary>
    /// <remarks>
    /// <para>
    /// The language is the project whose root concept the document's root is, which is why a document of the shell's
    /// own languages has no tests to run: those languages are compiled code, and their tests are C# in this
    /// repository.
    /// </para>
    /// <para>
    /// The runner is the same one CI uses, so a row here and a row in the build are the same fact. The evaluation
    /// rules come from the document's own tab, because a language project carries no evaluation aspect and the tab
    /// already resolved which registry belongs to this language.
    /// </para>
    /// </remarks>
    private void RunActiveTests()
    {
        if (!Allows(ShellCapability.Tests))
        {
            return;
        }

        _error = null;

        if (_documents.Active is not { } document)
        {
            return;
        }

        if (_language.ProjectFor(document.Model) is not { } project)
        {
            _error = "This document's language is not a project opened from a folder, so it has no tests folder.";

            UpdateStatus();

            return;
        }

        TestReport tests;

        try
        {
            tests = TestRunner.Run(project, document.Evaluation);
        }
        catch (Exception exception) when (exception is ConceptDefinitionException or ModelFormatException)
        {
            // A tests folder that holds something which is not a test is a broken suite rather than a result, so it
            // is reported as the command's answer instead of being turned into a row.
            _error = exception.Message;

            UpdateStatus();

            return;
        }

        document.Tests = tests;
        document.Output = TestOutput.Report(tests, project);

        RevealOutput();
        UpdateStatus();
    }

    private void OnRunInputsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        RunActiveDocument();
    }

    private void RunActiveDocument()
    {
        if (!Allows(ShellCapability.Run))
        {
            return;
        }

        if (_documents.Active is not { } document)
        {
            return;
        }

        // A language that defines no interpreter cannot be run at all, and saying so is better than running it
        // against another language's rules and reporting a missing rule for every concept.
        if (document.Evaluation is not { } rules)
        {
            _error = "This document's language defines no interpreter, so it cannot be run.";
            UpdateStatus();

            return;
        }

        _error = null;
        document.Output = DocumentRun.Of(document.Root, rules, RunInputsInput.Text);

        RevealOutput();
        UpdateStatus();
    }

    /// <summary>Generates the document in front with <paramref name="generator"/> and shows the text.</summary>
    private void RunGenerator(Projectional.Language.TextGenerator generator)
    {
        if (_documents.Active is not { } document)
        {
            return;
        }

        _error = null;

        var generated = DocumentGenerate.Generate(document.Root, generator);

        document.Generated = generated.Text;
        document.Output = DocumentGenerate.Report(generated, generator);

        RevealOutput();
        UpdateStatus();
    }

    private void OnClearOutput(object? sender, RoutedEventArgs e)
    {
        if (_documents.Active is { } document)
        {
            document.Output = null;
            document.Tests = null;
        }

        ShowOutput();
        UpdateStatus();
    }

    /// <summary>Puts the caret on the node a trace line came from.</summary>
    /// <remarks>
    /// The pick is cleared afterwards, so the same row is clickable twice: a trace is something a user reads back
    /// and forth between the document and the panel, unlike a problem, which is a state they are working through.
    /// </remarks>
    private void OnRunRowChosen(object? sender, RoutedEventArgs e)
    {
        if (_documents.Active is not { } document)
        {
            return;
        }

        if (document.Tests is { } tests)
        {
            OpenChosenTest(tests);

            return;
        }

        if (RunOutput.SelectedItem is not RunRow { Place: { } place }
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        document.Session.Select(Selection.At(place));
        editor.ScrollToCaret();
        editor.Focus();

        RunOutput.SelectedItem = null;
    }

    /// <summary>Opens the test document whose row the user chose.</summary>
    /// <remarks>
    /// <para>
    /// A test row has no address to jump to: it is about a document, and the document it names is not the one in
    /// front. What a row is worth clicking for is the failing test itself, which is written in the test language -
    /// so it is opened with that language's project and notations rather than the shell's, exactly as a language's
    /// own documents are opened with their project's.
    /// </para>
    /// <para>
    /// The row is resolved by position, which is the contract <see cref="TestOutput"/> keeps: one row per result, in
    /// the runner's order.
    /// </para>
    /// </remarks>
    private void OpenChosenTest(TestReport tests)
    {
        var index = RunOutput.SelectedIndex;

        if (index < 0 || index >= tests.Results.Count)
        {
            return;
        }

        var result = tests.Results[index];

        RunOutput.SelectedItem = null;

        if (_documents.Active is not { } document || _language.ProjectFor(document.Model) is not { } project)
        {
            return;
        }

        var path = Path.Combine(project.Directory, TestRunner.Folder, result.Test + DocumentFile.Extension);

        _error = null;

        if (DocumentFile.TryLoad(path, TestingLanguage.NewProject(), out var error) is not { } model)
        {
            _error = error;

            UpdateStatus();

            return;
        }

        OpenDocument(model, TestingNotations.Registry, path);
    }

    /// <summary>Registers the framework's catalogue and the intentions the document's languages ship with.</summary>
    /// <param name="document">The document whose session gains them.</param>
    /// <remarks>
    /// <para>
    /// This is the whole of what a host writes: four providers, registered once. The first is the framework's own
    /// catalogue, which derives rename, duplicate, delete, the moves, the two sorts of insertion, sorting, the two
    /// selections, wrap, replace and reset from the shape of the caret's place, so a document gains them without its
    /// language doing anything. The other three are the languages' answers to "what is meaningful here": the
    /// expression vocabulary's negation, the rules language's "accept this rule", and the structure language's
    /// "make abstract".
    /// </para>
    /// <para>
    /// The catalogue is registered as the <em>host's</em> rather than as a language's, because the app is what chose
    /// it: the list keeps host entries in front of a language's, which puts the generic operations above the
    /// language-specific ones. That order is also why the expression package no longer ships "wrap in parentheses" -
    /// the catalogue's own search offers <c>Wrap in "Parenthesized"</c> for every language, and one operation must not
    /// appear twice.
    /// </para>
    /// <para>
    /// The three language providers are registered as <see cref="IntentionSource.Language"/>, because that is what
    /// they are. A provider that is not about the open document answers <see cref="IntentionAvailability.Absent"/> at
    /// every caret, so registering all three for every document is how a mixed set of languages is spelled.
    /// </para>
    /// </remarks>
    private static void RegisterIntentions(DocumentTab document)
    {
        document.Session.AddIntentions(StandardIntentions.Provider);
        document.Session.AddIntentions(ExpressionsIntentions.Provider, IntentionSource.Language);
        document.Session.AddIntentions(RulesIntentions.Provider, IntentionSource.Language);
        document.Session.AddIntentions(StructureIntentions.Provider, IntentionSource.Language);
    }

    /// <summary>Applies a theme choice to the window, its chrome and every tab's control.</summary>
    /// <param name="choice">
    /// <see cref="ThemeVariant.Default"/> to follow the operating system, or a variant to pin.
    /// </param>
    /// <remarks>
    /// <para>
    /// The variant is <em>requested</em> rather than assumed, so picking light on a dark desktop gives a
    /// light window rather than a light document inside a dark frame - the palette and the tab strip are
    /// painted from the same variant, which is why one call does both.
    /// </para>
    /// <para>
    /// A request may be resolved later than this call, which is what
    /// <see cref="OnActualThemeVariantChanged"/> is for. That handler only acts while the choice is
    /// <see cref="ThemeVariant.Default"/>, and this method never re-raises the event by applying colours,
    /// so the two cannot chase each other.
    /// </para>
    /// </remarks>
    private void ApplyThemeChoice(ThemeVariant choice)
    {
        _themeChoice = choice;
        RequestedThemeVariant = choice;

        ApplyTheme(SyntaxTheme.Of(ActualThemeVariant));

        FollowSystemItem.IsChecked = choice == ThemeVariant.Default;
        LightThemeItem.IsChecked = choice == ThemeVariant.Light;
        DarkThemeItem.IsChecked = choice == ThemeVariant.Dark;

        UpdateStatus();
    }

    /// <summary>Repaints the window when the desktop switches between light and dark.</summary>
    /// <remarks>
    /// Only while the window follows the system: a user who pinned a theme keeps it when the desktop
    /// changes, which is the whole difference between the three menu entries. Colours are all that is
    /// applied here - the request is already the one the user made - so this cannot re-enter itself.
    /// </remarks>
    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_themeChoice != ThemeVariant.Default)
        {
            return;
        }

        ApplyTheme(SyntaxTheme.Of(ActualThemeVariant));
        UpdateStatus();
    }

    private void ApplyTheme(SyntaxTheme theme)
    {
        _theme = theme;

        // The window chrome follows the document rather than the operating system, because the shell
        // offers a theme switch: asking for a dark palette and getting a white tab strip would be a
        // bug the user can see.
        Background = theme.Chrome;

        foreach (var tab in _tabs.Values)
        {
            if (EditorOf(tab) is { } editor)
            {
                ApplyThemeTo(editor, theme);
            }
        }
    }

    private static void ApplyThemeTo(ProjectionalEditorControl editor, SyntaxTheme theme)
    {
        // Ten properties rather than one, because the control refuses to guess what surface its
        // palette is meant for: a dark palette is only readable on a dark background.
        editor.Palette = theme.Palette;
        editor.Background = theme.Background;
        editor.CaretBrush = theme.Caret;
        editor.HighlightBrush = theme.Highlight;
        editor.GutterBackground = theme.Gutter;
        editor.LineNumberBrush = theme.LineNumber;
        editor.MarkerColors = theme.Markers;

        // The overlays as well, or a dark page gets white boxes on it: the completion list, the quick-fix
        // list, the marker tooltip and the bulb are painted over the document and are read against it.
        editor.ListPalette = theme.Lists;
        editor.TipPalette = theme.Tip;
        editor.BulbBrush = theme.Bulb;
    }

    private void UpdateStatus()
    {
        foreach (var (document, tab) in _tabs)
        {
            if (tab.Header is DocumentTabHeader header)
            {
                header.SetTitle(document.Title, document.IsDirty);
            }
        }

        // A window with nothing open says so in the page rather than leaving a white rectangle behind.
        EmptyState.IsVisible = _documents.Active is null;

        StatusDocument.Text = _documents.Active is not { } active
            ? "No document open"
            : $"{active.Title}{FormatFile(active.FilePath)} | {(_documents.AnyUnsaved ? "unsaved changes" : "saved")}";

        // The task bar and Alt-Tab are part of the shell too, and they said "Projectional Workbench" whatever
        // was open - including whether it had unsaved changes.
        Title = _documents.Active is { } shown
            ? $"{(shown.IsDirty ? "*" : string.Empty)}{shown.Title} - {WindowTitle}"
            : WindowTitle;

        // The message goes last and is not cleared here: it is the answer to the command the user just
        // ran, and the next command clears it, so it cannot go stale while the user reads it. It is also
        // trimmed when it is long, so the tooltip is where the whole sentence lives.
        StatusText.Text = _error ?? string.Empty;
        ToolTip.SetTip(StatusText, string.IsNullOrEmpty(_error) ? null : _error);

        UpdateStatusDetail();

        UpdateCommands();
        UpdatePanelCommands();
        UpdateRepresentations();
        UpdateRunCommand();
        UpdateGenerators();
        UpdateExportCommand();
    }

    /// <summary>
    /// Fills the right-hand end of the status line: where the caret stands, which language this is, the theme
    /// and the size - each part only when it has something to say.
    /// </summary>
    private void UpdateStatusDetail()
    {
        var parts = new List<string>(4);

        if (_documents.Active is { } document)
        {
            if (CaretSpot.Of(document.Session) is { } spot)
            {
                parts.Add(spot.ToString());
            }

            parts.Add(LanguageNameOf(document));
        }

        parts.Add(ThemeText());
        parts.Add($"{_fontSize:0} px");

        StatusDetail.Text = string.Join("  |  ", parts);
    }

    /// <summary>Says where the caret went, as the control of the document in front reported it.</summary>
    private void OnCaretMoved(object? sender, EventArgs e) => UpdateStatusDetail();

    /// <summary>What language the document in front is written in, as the status bar names it.</summary>
    /// <param name="document">The document in front.</param>
    /// <remarks>
    /// An opened language project answers with its own name. The shell's built-in languages have no project to
    /// ask, so the root concept's key prefix stands in - <c>Rules</c> for the sample ruleset, <c>Json</c> for an
    /// imported file - which is the key the language declared its concepts under, not a guess.
    /// </remarks>
    private string LanguageNameOf(DocumentTab document)
    {
        if (_language.ProjectFor(document.Model) is { } project)
        {
            return project.Name;
        }

        var key = document.Root.Concept.Key;
        var dot = key.IndexOf('.', StringComparison.Ordinal);

        return dot > 0 ? key[..dot] : key;
    }

    /// <summary>Says which editing commands the document in front can carry out.</summary>
    /// <remarks>
    /// Undo and redo are facts of the history rather than of the document, so they follow it: an entry that is
    /// always enabled teaches the user that pressing it does nothing.
    /// </remarks>
    private void UpdateCommands()
    {
        var session = _documents.Active?.Session;

        UndoItem.IsEnabled = session?.History.CanUndo == true;
        RedoItem.IsEnabled = session?.History.CanRedo == true;
        CutItem.IsEnabled = session is not null;
        CopyItem.IsEnabled = session is not null;
        PasteItem.IsEnabled = session is not null;
        SelectAllItem.IsEnabled = session is not null;
        RenameDocumentItem.IsEnabled = session is not null;
    }

    /// <summary>Says which panels are in the area, and which ones could be.</summary>
    private void UpdatePanelCommands()
    {
        var problems = _documents.Active?.Problems ?? [];

        OutputPanelItem.IsEnabled = _documents.Active?.Output is not null;
        OutputPanelItem.IsChecked = IsPanelOpen(OutputTab);

        ProblemsPanelItem.IsEnabled = problems.Count > 0;
        ProblemsPanelItem.IsChecked = IsPanelOpen(ProblemsTab);

        UsagesPanelItem.IsEnabled = _usageTarget is not null;
        UsagesPanelItem.IsChecked = IsPanelOpen(UsagesTab);

        // The count is shown while the tab is not: a user who closed it still has to know the document is not
        // clean, and the status bar is where the rest of the document's state lives.
        StatusProblems.IsVisible = problems.Count > 0 && !IsPanelOpen(ProblemsTab);
        StatusProblems.Content = problems.Count == 1 ? "1 problem" : $"{problems.Count} problems";
    }

    /// <summary>The editor of the document in front, or <see langword="null"/> when there is none.</summary>
    private ProjectionalEditorControl? ActiveEditor() =>
        _documents.Active is { } document && _tabs.TryGetValue(document, out var tab) ? EditorOf(tab) : null;

    /// <summary>Says whether the document in front can be run, and why not when it cannot.</summary>
    /// <remarks>
    /// A disabled entry states a fact the user cannot otherwise see - that the language in front defines no
    /// interpreter - so the reason goes on the tooltip as well as into the status bar when F5 is pressed: the command
    /// is refused rather than offered, and "why" must have an answer somewhere.
    /// </remarks>
    private void UpdateRunCommand()
    {
        var runnable = _documents.Active?.Evaluation is not null;
        var project = _documents.Active is { } active ? _language.ProjectFor(active.Model) : null;

        RunDocumentItem.IsEnabled = runnable;
        RunTestsItem.IsEnabled = project is not null;
        ClearOutputItem.IsEnabled = _documents.Active?.Output is not null;
        ToolTip.SetTip(
            RunDocumentItem,
            runnable
                ? "Runs the document with its language's interpreter."
                : "This document's language defines no interpreter.");
        ToolTip.SetTip(
            RunTestsItem,
            project is null
                ? "Only a document of a language opened from a folder can run that language's tests."
                : $"Runs the tests folder of '{project.Name}' and reports one row per test.");
    }

    /// <summary>Fills the generate submenu from the active document's generators, and says whether saving is possible.</summary>
    /// <remarks>
    /// A language decides how many generators it has, so the menu is built from the document rather than written in
    /// XAML - the same rule the representation submenu follows, and the reason there is no "the" generate command. It
    /// is rebuilt whenever the status bar is, which is whenever anything about the document changed.
    /// </remarks>
    private void UpdateGenerators()
    {
        var generators = _documents.Active?.Generators ?? [];

        GenerateItem.IsEnabled = generators.Count > 0;
        GenerateItem.ItemsSource = generators
            .Select(generator => new MenuItem { Header = generator.Name, Command = new RunGeneratorCommand(this, generator) })
            .ToList();

        SaveGeneratedItem.IsEnabled = _documents.Active?.Generated is not null;
    }

    /// <summary>Offers one export command per language that came from a folder.</summary>
    /// <remarks>
    /// Only languages opened from a folder can be exported: the built-in ones are already code, so exporting them
    /// would generate what the repository already compiles. Which language a document belongs to cannot be read off
    /// the document - a document may borrow concepts - so the menu names the languages instead of guessing one.
    /// </remarks>
    private void UpdateExportCommand()
    {
        var opened = _language.Opened;

        ExportLanguageItem.IsEnabled = opened.Count > 0;
        ExportLanguageItem.ItemsSource = opened
            .Select(language => new MenuItem { Header = language.Name, Command = new ExportLanguageCommand(this, language) })
            .ToList();

        ToolTip.SetTip(
            ExportLanguageItem,
            opened.Count > 0
                ? "Writes the generated C# and the compiled assembly of a language opened from a folder."
                : "No language has been opened from a folder.");
    }

    /// <summary>A menu entry that exports one opened language.</summary>
    private sealed class ExportLanguageCommand : System.Windows.Input.ICommand
    {
        private readonly MainWindow _window;
        private readonly LanguageProject _language;

        internal ExportLanguageCommand(MainWindow window, LanguageProject language)
        {
            _window = window;
            _language = language;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _window.ExportLanguage(_language);
    }

    /// <summary>Writes one opened language as the C# a package is made of, compiling it on the way.</summary>
    /// <remarks>
    /// The shell compiles rather than offering the source alone, because this command's whole purpose is shipping a
    /// language that was prototyped in the editor; a caller that wanted only the source has the library. A compilation
    /// failure is a message rather than a crash - it means the export cannot spell something this language declares,
    /// which is exactly what the user has to read.
    /// </remarks>
    private async void ExportLanguage(LanguageProject language)
    {
        if (!Allows(ShellCapability.Export))
        {
            return;
        }

        _error = null;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Choose where to write {language.Name}",
            AllowMultiple = false,
        });

        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } directory)
        {
            return;
        }

        try
        {
            var result = LanguageExporter.Export(
                language,
                LanguageExporter.NamespaceFor(language.Name),
                ExportOutput.Assembly);

            var written = LanguageExportWriter.Write(result, directory);

            _error = $"{language.Name} exported: {written.Count} files written to {directory}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExportCompilationException)
        {
            _error = $"Could not export {language.Name}: {exception.Message}";
        }

        UpdateStatus();
    }

    /// <summary>A menu entry that generates one document with one generator.</summary>
    private sealed class RunGeneratorCommand : System.Windows.Input.ICommand
    {
        private readonly MainWindow _window;
        private readonly Projectional.Language.TextGenerator _generator;

        internal RunGeneratorCommand(MainWindow window, Projectional.Language.TextGenerator generator)
        {
            _window = window;
            _generator = generator;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _window.RunGenerator(_generator);
    }

    /// <summary>Writes the last generated text to a file the user picks.</summary>
    /// <remarks>
    /// The framework returns text and the host decides where it goes (open question 12), so writing the file is the
    /// shell's job and not the generator's: a language component that wrote files would have to guess a path, an
    /// encoding and a newline convention, and a host that embeds it could not decline.
    /// </remarks>
    private async void OnSaveGenerated(object? sender, RoutedEventArgs e)
    {
        if (_documents.Active is not { Generated: { } text } document)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save generated text",
            SuggestedFileName = $"{document.Title}.txt",
            FileTypeChoices = [new FilePickerFileType("Text") { Patterns = ["*.txt"] }],
        });

        if (file is null)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(file.Path.LocalPath, text);
            _error = $"Generated text written to {file.Name}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _error = exception.Message;
        }

        UpdateStatus();
    }

    /// <summary>The theme as the status bar names it, saying so when the desktop chose it.</summary>
    /// <remarks>
    /// "dark theme (system)" and "dark theme" describe different states: the first will follow the
    /// desktop when it changes, the second will not. Without the suffix the user cannot tell which one
    /// they are looking at, and picking dark by hand while the desktop is dark would look like nothing
    /// happened.
    /// </remarks>
    private string ThemeText() =>
        _themeChoice == ThemeVariant.Default
            ? $"{(_theme.IsDark ? "dark" : "light")} theme (system)"
            : $"{(_theme.IsDark ? "dark" : "light")} theme";

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
            && EditorOf(tab) is { } editor)
        {
            editor.Refresh();
        }
    }

    /// <summary>Opens the documents the user dragged onto the window.</summary>
    /// <remarks>
    /// A saved model opens as a document; anything else is offered to the importers, which is the same command
    /// the File menu runs and speaks its own refusal when it cannot read the file. Which of the two applies is
    /// the file's extension and not a guess: a <c>.mps</c> file is a model this app wrote.
    /// </remarks>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not { Length: > 0 } files)
        {
            return;
        }

        e.Handled = true;

        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is not { } path)
            {
                _error = "A dropped file has no path this app can read.";
                continue;
            }

            if (path.EndsWith(DocumentFile.Extension, StringComparison.OrdinalIgnoreCase))
            {
                TryOpenPath(path);
            }
            else
            {
                ImportPath(path);
            }
        }

        UpdateStatus();
    }

    /// <summary>Says "copy" while files are dragged over the window, so the gesture looks possible before it is tried.</summary>
    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 }
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private static string FormatFile(string? filePath) =>
        filePath is null ? string.Empty : $" ({Path.GetFileName(filePath)})";

    private void FocusActiveDocument()
    {
        if (_documents.Active is { } document && _tabs.TryGetValue(document, out var tab))
        {
            EditorOf(tab)?.Focus();
        }
    }

    private void OnNewDocument(object? sender, RoutedEventArgs e)
    {
        if (!Allows(ShellCapability.CreateModels))
        {
            return;
        }

        _error = null;
        OpenDocument(PayrollSample.NewEmptyModel(), _language.Notations, typing: WorkbenchLanguage.Typing);
    }

    /// <summary>Opens an empty concept document, so the declared path can be tried without setup.</summary>
    /// <remarks>
    /// The structure language is loaded into this app like any other, so this is only a convenience: the document
    /// it opens is a model written in that language, editable and saveable by the same editor as everything else.
    /// </remarks>
    private void OnNewConcept(object? sender, RoutedEventArgs e)
    {
        _error = null;

        OpenDocument(
            _language.NewConceptDefinition(),
            _language.Notations,
            typing: WorkbenchLanguage.Typing);
    }

    /// <summary>Opens an empty notation, which is one half of a language definition.</summary>
    private void OnNewNotation(object? sender, RoutedEventArgs e)
    {
        _error = null;

        OpenDocument(
            _language.NewNotationDefinition(),
            _language.Notations,
            typing: WorkbenchLanguage.Typing);
    }

    /// <summary>Opens an empty enumeration, which is the other half of what a structure document can hold.</summary>
    private void OnNewEnumeration(object? sender, RoutedEventArgs e)
    {
        _error = null;

        OpenDocument(
            _language.NewEnumerationDefinition(),
            _language.Notations,
            typing: WorkbenchLanguage.Typing);
    }

    /// <summary>Opens a folder as a language and takes it into this shell's set.</summary>
    /// <remarks>
    /// The language's own two models open as documents afterwards, which is where the user edits them; new
    /// documents of the language can then be created from the same registries. A refused language leaves the shell
    /// exactly as it was and says why, because a folder is user input.
    /// </remarks>
    private async void OnOpenLanguage(object? sender, RoutedEventArgs e)
    {
        _error = null;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open a language project",
            AllowMultiple = false,
        });

        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } directory)
        {
            return;
        }

        if (LanguageProject.Open(directory, out var error) is not { } language)
        {
            _error = error;
            UpdateStatus();

            return;
        }

        _error = null;

        OpenLanguage(language);

        UpdateStatus();
    }

    /// <summary>Takes <paramref name="language"/> into the shell and opens every document that defines it.</summary>
    /// <param name="language">The project, already loaded from its folder.</param>
    /// <returns>Whether the shell can now show documents of that language.</returns>
    /// <remarks>
    /// <para>
    /// Every definition document of the folder opens, because that is where the user edits the language: one concept
    /// or enumeration per file, one notation per file, one rule or named type per file.
    /// </para>
    /// <para>
    /// Internal rather than private because the screenshot harness drives the shell through this seam: a folder
    /// picker cannot be driven from a harness, and leaving the rest inline would mean a picture of "the tests ran"
    /// arranged by hand rather than reached through the command. It is not public for the same reason
    /// <see cref="OpenDocument"/> is not - the shell is a library only once M6 extracts it.
    /// </para>
    /// </remarks>
    internal bool OpenLanguage(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (!_language.TryAdd(language, out var error))
        {
            _error = error;

            return false;
        }

        foreach (var folder in new[] { LanguageProject.StructureFolder, LanguageProject.EditorFolder, LanguageProject.TypesFolder, LanguageProject.ConstraintsFolder, LanguageProject.ScopesFolder, LanguageProject.IntentionsFolder })
        {
            var path = Path.Combine(language.Directory, folder);

            if (!System.IO.Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in System.IO.Directory
                .EnumerateFiles(path, "*.mps", SearchOption.TopDirectoryOnly)
                .OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal))
            {
                if (DocumentFile.TryLoad(file, _language.NewProject(), out _) is { } model)
                {
                    OpenDocument(model, _language.Notations, file, WorkbenchLanguage.Emphasis, WorkbenchLanguage.Typing);
                }
            }
        }

        // A language whose folders hold no definition document opens no tab, so the project list is the only
        // place that learns it joined.
        ShowProject();

        return true;
    }

    /// <summary>Creates a document of one of the languages this shell knows.</summary>
    /// <param name="sender">The menu entry.</param>
    /// <param name="e">The event.</param>
    /// <remarks>
    /// Only the languages that have joined the set are offered, so the menu cannot create a document of a language
    /// the shell would then be unable to load.
    /// </remarks>
    private void OnNewLanguageDocument(object? sender, RoutedEventArgs e)
    {
        _error = null;

        var opened = _language.Opened.FirstOrDefault(language => WorkbenchLanguage.Rootable(language) is not null);

        if (opened is null || WorkbenchLanguage.NewDocument(opened) is not { } model)
        {
            _error = "No opened language declares a concept a document could start with.";
            UpdateStatus();

            return;
        }

        OpenDocument(model, _language.Notations, typing: WorkbenchLanguage.Typing, types: opened.Types, constraints: opened.Constraints, scopes: opened.Scopes, intentions: opened.Intentions);
    }

    private async void OnOpenDocument(object? sender, RoutedEventArgs e)
    {
        if (!Allows(ShellCapability.OpenFiles))
        {
            return;
        }

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

        TryOpenPath(path);
    }

    /// <summary>Imports a file as a new document, using whichever language in this window can read it.</summary>
    /// <remarks>
    /// Import is not open: the model that comes out is not linked to the file it came from, so it opens as an untitled
    /// document that is already clean - nothing has been edited - and saving writes a <c>.mps</c> document or the
    /// generated text, never the original.
    /// </remarks>
    private async void OnImportDocument(object? sender, RoutedEventArgs e)
    {
        if (!Allows(ShellCapability.Import))
        {
            return;
        }

        _error = null;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import a file",
            AllowMultiple = false,
            FileTypeFilter = [TextFileType()],
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

        ImportPath(path);
    }

    /// <summary>Imports the file at <paramref name="path"/>, reporting the importer's place when it refuses.</summary>
    /// <remarks>
    /// <para>
    /// A refusal points at a line and a column of the file the user picked, not at a node: there is no model to point
    /// at yet. That is why the message is built from the diagnostic rather than from the model, and why a refused file
    /// leaves no tab behind at all.
    /// </para>
    /// <para>
    /// Internal rather than private because the screenshot harness drives it, the same way it drives
    /// <see cref="OpenLanguage"/>: a file picker cannot be clicked from a harness, and the alternative would be a
    /// picture of an imported document arranged by hand rather than reached through the command.
    /// </para>
    /// </remarks>
    internal void ImportPath(string path)
    {
        _error = null;

        string text;

        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException failure)
        {
            _error = $"Could not read {Path.GetFileName(path)}: {failure.Message}";
            UpdateStatus();
            return;
        }
        catch (UnauthorizedAccessException)
        {
            _error = $"Could not read {Path.GetFileName(path)}: the file is not readable.";
            UpdateStatus();
            return;
        }

        if (_language.TryImport(Path.GetFileNameWithoutExtension(path), text, out var reason) is { } imported)
        {
            _error = ImportSummary.Of(Path.GetFileName(path), imported.Language.Name, imported.Result);

            OpenImported(imported);

            return;
        }

        if (ReferenceLanguages.Detect(path, text) is not { } language)
        {
            _error = reason is not null
                ? $"No language in this window could read {Path.GetFileName(path)}: {reason}."
                : $"{Path.GetFileName(path)} is neither JSON nor XML, so there is no importer for it.";

            UpdateStatus();

            return;
        }

        var outcome = ReferenceImport.Import(language, Path.GetFileNameWithoutExtension(path), text);

        if (!outcome.Succeeded)
        {
            _error = outcome.Refusal is { } refusal
                ? $"{language.Name} refused {Path.GetFileName(path)} at {refusal.Line}:{refusal.Column} - {refusal.Message}"
                : $"{language.Name} refused {Path.GetFileName(path)}.";

            UpdateStatus();

            return;
        }

        _error = ImportSummary.Of(Path.GetFileName(path), language.Name, outcome.Result);

        OpenDocument(
            outcome.Model,
            language.Notations,
            constraints: ConstraintSet.Empty,
            intentions: language.Intentions,
            generators: language.Generators,
            checks: language.Checks);
    }

    /// <summary>Opens <paramref name="imported"/> with the aspects of the language that read it.</summary>
    /// <param name="imported">The document, and the opened project whose importer produced it.</param>
    /// <remarks>
    /// The aspects are that language's own - its types, constraints, scopes, intentions and generators - because an
    /// imported document must behave exactly like one created from the menu. The notations are the shell's combined
    /// registry for the reason a new document of an opened language uses it: a document may draw its own concepts and
    /// the definitions it borrows.
    /// </remarks>
    private void OpenImported(ImportedDocument imported)
    {
        ArgumentNullException.ThrowIfNull(imported);

        OpenDocument(
            imported.Model,
            _language.Notations,
            typing: WorkbenchLanguage.Typing,
            types: imported.Language.Types,
            constraints: imported.Language.Constraints,
            scopes: imported.Language.Scopes,
            intentions: imported.Language.Intentions,
            generators: imported.Language.Generators);
    }

    /// <summary>Opens the model at <paramref name="path"/>, reporting why it could not when it cannot.</summary>
    /// <remarks>
    /// The picker and the recent-files menu both end here, so "open a file" has one implementation: a second one would
    /// be a second place where a missing file, an already-open document and a language mismatch are handled.
    /// </remarks>
    private void TryOpenPath(string path)
    {
        _error = null;

        if (_documents.Find(path) is { } alreadyOpen)
        {
            Select(alreadyOpen);
            UpdateStatus();

            return;
        }

        if (DocumentFile.TryLoad(path, _language.NewProject(), out var error) is not { } model)
        {
            _error = $"Could not open {Path.GetFileName(path)}: {error}";

            // A file that can no longer be opened is exactly what a recent list must forget, or the menu keeps
            // offering a click that fails.
            ForgetRecent(path);
            UpdateStatus();

            return;
        }

        RememberRecent(path);
        OpenDocument(model, RulesLanguage.CreateNotations(model.Concepts), filePath: path, emphasis: RulesLanguage.Emphasis, typing: RulesLanguage.TypingRules);
    }

    /// <summary>Puts <paramref name="path"/> at the top of the recent list and remembers it.</summary>
    private void RememberRecent(string path)
    {
        _recent = _recent.With(path);

        _recentStore.Write(_recent.Paths);
        RebuildRecentMenu();
    }

    /// <summary>Drops <paramref name="path"/> from the recent list, for a file that can no longer be opened.</summary>
    private void ForgetRecent(string path)
    {
        if (_recent.Paths.Count == 0)
        {
            return;
        }

        _recent = _recent.Without(path);

        _recentStore.Write(_recent.Paths);
        RebuildRecentMenu();
    }

    /// <summary>Rebuilds File ▸ Recent from the list.</summary>
    /// <remarks>
    /// Rebuilt rather than bound, because the entries carry a path each and a menu is the cheapest place to keep that:
    /// an item per path, closing over the path it was made for. An empty list disables the parent entry instead of
    /// hiding it, so the menu does not change shape depending on what the user did last.
    /// </remarks>
    private void RebuildRecentMenu()
    {
        RecentFilesItem.Items.Clear();
        RecentFilesItem.IsEnabled = !_recent.IsEmpty;

        foreach (var path in _recent.Paths)
        {
            var item = new MenuItem { Header = Path.GetFileName(path) };

            // The header is the file name, so the full path is what the tooltip is for.
            ToolTip.SetTip(item, path);

            item.Click += (_, _) => TryOpenPath(path);

            RecentFilesItem.Items.Add(item);
        }
    }

    private async void OnSaveDocument(object? sender, RoutedEventArgs e)
    {
        if (!Allows(ShellCapability.Save))
        {
            return;
        }

        _error = null;

        if (_documents.Active is { } document)
        {
            await SaveAsync(document);
        }
    }

    private async void OnSaveDocumentAs(object? sender, RoutedEventArgs e)
    {
        if (!Allows(ShellCapability.Save))
        {
            return;
        }

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
            // A file that has just been written is a file worth offering again next time.
            RememberRecent(path);
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

    /// <summary>The filter for importing: the built-in formats, and then every file.</summary>
    /// <remarks>
    /// All files are offered beside the built-in formats, because a declared importer names no extension: the file a
    /// language can read is decided by its rules, not by three letters a dialog would have to know in advance.
    /// </remarks>
    private static FilePickerFileType TextFileType() => new("JSON, XML, TOML, HOCON, YAML or a file a language reads")
    {
        Patterns =
        [
            .. ReferenceLanguages.All.SelectMany(language => language.Extensions.Select(extension => $"*{extension}")),
            "*",
        ],
    };

    private void OnCloseDocument(object? sender, RoutedEventArgs e) => CloseActiveDocument();

    private void OnLightTheme(object? sender, RoutedEventArgs e) => ApplyThemeChoice(ThemeVariant.Light);

    private void OnDarkTheme(object? sender, RoutedEventArgs e) => ApplyThemeChoice(ThemeVariant.Dark);

    private void OnFollowSystemTheme(object? sender, RoutedEventArgs e) =>
        ApplyThemeChoice(ThemeVariant.Default);

    private void OnToggleLineNumbers(object? sender, RoutedEventArgs e)
    {
        _showLineNumbers = LineNumbersItem.IsChecked;

        foreach (var tab in _tabs.Values)
        {
            if (EditorOf(tab) is { } editor)
            {
                editor.ShowLineNumbers = _showLineNumbers;
            }
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    private void OnIncreaseFontSize(object? sender, RoutedEventArgs e) => ApplyFontSize(_fontSize + FontSizeStep);

    private void OnDecreaseFontSize(object? sender, RoutedEventArgs e) => ApplyFontSize(_fontSize - FontSizeStep);

    private void OnResetFontSize(object? sender, RoutedEventArgs e) => ApplyFontSize(DefaultFontSize);

    /// <summary>Puts every table column in the active document back to the width its content asks for.</summary>
    /// <remarks>
    /// Column widths are view state, so this is not an edit: it opens no transaction, creates no undo
    /// step and does not mark the document modified. It is here rather than on the control because a
    /// reader who has dragged a column too wide needs a way back that does not require aiming at the
    /// boundary again.
    /// </remarks>
    private void OnResetColumnWidths(object? sender, RoutedEventArgs e)
    {
        if (_documents.Active is not { } document
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        document.Session.ResetColumnWidths();

        editor.InvalidateMeasure();
        editor.InvalidateVisual();
    }

    /// <summary>Routes the zoom gestures that a <c>MenuItem.HotKey</c> cannot express.</summary>
    /// <remarks>
    /// On a normal keyboard "+" is <see cref="Key.OemPlus"/> pressed <em>with</em> Shift, so a single
    /// registered gesture would either miss the shifted press or require the user to hold Shift for a
    /// key that is printed as "=". Matching here accepts both, and the numpad keys as well. Ctrl+0 is
    /// included because the top-row zero and the numpad zero are different keys to Avalonia.
    /// </remarks>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // F5 is the run gesture and carries no modifier, so it is answered before the zoom block below - which
        // deliberately ignores every key that is not Ctrl.
        if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            RunActiveDocument();

            return;
        }

        // Shift+F6 is the rename gesture, MPS' own. It opens the bar because the new name is the user's to type -
        // the intention that jumps into the name cell stays what it was.
        if (e.Key == Key.F6 && e.KeyModifiers == KeyModifiers.Shift)
        {
            e.Handled = true;
            OpenRenameBar();

            return;
        }

        // Alt+F7 is the find-usages gesture. It belongs to the window for the same reason F5 does: the answer is a
        // panel the shell owns, not a list the editor control can paint over the document.
        if (e.Key == Key.F7 && e.KeyModifiers == KeyModifiers.Alt)
        {
            e.Handled = true;
            ShowActiveUsages();

            return;
        }

        if ((e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.OemPlus or Key.Add:
                ApplyFontSize(_fontSize + FontSizeStep);
                break;
            case Key.OemMinus or Key.Subtract:
                ApplyFontSize(_fontSize - FontSizeStep);
                break;
            case Key.D0 or Key.NumPad0:
                ApplyFontSize(DefaultFontSize);
                break;

            // Ctrl+H is the replace gesture every desk editor has, and the menu prints it. It is answered here
            // rather than declared as a HotKey for the same reason the zoom keys are: one place decides.
            case Key.H:
                OpenFindBar(withReplace: true);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Applies one font size to every open document.</summary>
    /// <remarks>
    /// The size belongs to the shell, not to a document: a user who zooms to read one tab expects the
    /// next one to open at the same size. It is clamped rather than refused, so holding the gesture
    /// down stops at the limit instead of failing silently.
    /// </remarks>
    private void ApplyFontSize(double size)
    {
        var clamped = Math.Clamp(size, MinimumFontSize, MaximumFontSize);

        if (clamped == _fontSize)
        {
            return;
        }

        _fontSize = clamped;

        foreach (var tab in _tabs.Values)
        {
            if (EditorOf(tab) is { } editor)
            {
                editor.FontSize = _fontSize;
            }
        }

        UpdateStatus();
    }
}
