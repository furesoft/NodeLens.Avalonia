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
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Samples.Intentions;
using Projectional.Semantics;
using Projectional.Structures.Intentions;

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

    private readonly DocumentSet _documents = new();
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
    private bool _closeConfirmed;
    private bool _disposed;

    /// <summary>Creates the window and opens the sample document in it.</summary>
    public MainWindow()
    {
        InitializeComponent();

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

    private void OpenSample()
    {
        var model = PayrollSample.NewSampleModel();

        OpenDocument(model, _language.Notations, emphasis: WorkbenchLanguage.Emphasis, typing: WorkbenchLanguage.Typing);
    }

    private void OpenDocument(
        Model model,
        NotationRegistry notations,
        string? filePath = null,
        ICellEmphasis? emphasis = null,
        ITypingRules? typing = null,
        TypeRegistry? types = null,
        ConstraintSet? constraints = null)
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
            WorkbenchLanguage.InterpreterFor(model),
            WorkbenchLanguage.GeneratorsFor(model),
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

        var tab = new TabItem { Header = document.Title, Content = ScrollSurface(editor) };
        _documents.Add(document);
        _tabs.Add(document, tab);

        Documents.Items.Add(tab);
        Documents.SelectedItem = tab;

        ShowProblems();
        ShowOutput();
        ShowOutline();
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

        ShowProblems();
        ShowOutput();
        ShowOutline();
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

        ShowProblems();
        ShowOutput();
        ShowOutline();
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
            // from the new model; a run cannot, because the variables came from the user.
            document.Output = null;
            document.Generated = null;
        }

        ShowProblems();
        ShowOutput();
        ShowOutline();
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
        List<DiagnosticFix> fixes = [.. DefaultNames.Of(document.Model), .. DuplicateRuleNames.Of(document.Model)];

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
            editor.Session?.Scopes);

        editor.Markers = findings.Markers;
        editor.Fixes = findings.Fixes;

        // The panel reads the same list the squiggles are drawn from, so the two cannot disagree about
        // what was found - one of them would otherwise have to run the checks again.
        document.Problems = ProblemList.Of(document.Model, findings.Diagnostics);
    }

    /// <summary>Opens the find bar and puts the caret in it.</summary>
    private void OnFindText(object? sender, RoutedEventArgs e)
    {
        FindBar.IsVisible = true;
        FindInput.Focus();
        FindInput.SelectAll();
        UpdateFindCount();
    }

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

    /// <summary>Fills the problems panel from the active document, and hides it when there is nothing to report.</summary>
    private void ShowProblems()
    {
        var rows = _documents.Active?.Problems ?? [];

        Problems.ItemsSource = rows;
        Problems.SelectedItem = null;
        ProblemsPanel.IsVisible = rows.Count > 0;
        ProblemsHeader.Text = $"Problems ({rows.Count})";
    }

    /// <summary>Puts the caret on what a chosen row is about.</summary>
    /// <remarks>
    /// The caret is the app's one answer to "show me this": selecting the place scrolls it into view, focuses the
    /// editor and leaves the user where the row pointed - so the next keystroke, or Alt+Enter for the fix, acts on the
    /// finding rather than on wherever the caret happened to be.
    /// </remarks>
    private void OnProblemChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (Problems.SelectedItem is not ProblemEntry entry
            || _documents.Active is not { } document
            || !_tabs.TryGetValue(document, out var tab)
            || EditorOf(tab) is not { } editor)
        {
            return;
        }

        document.Session.Select(Selection.At(entry.Place));
        editor.ScrollToCaret();
        editor.Focus();
    }

    /// <summary>Runs the document in front and shows what came out.</summary>
    /// <remarks>
    /// The variables are read from the panel's line at the moment of the run, so a keystroke in that box is not a
    /// command: a user who is half way through typing <c>amount=1500</c> must not see a failure about <c>amoun</c>.
    /// </remarks>
    private void OnRunDocument(object? sender, RoutedEventArgs e) => RunActiveDocument();

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

        ShowOutput();
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

        ShowOutput();
        UpdateStatus();
    }

    private void OnClearOutput(object? sender, RoutedEventArgs e)
    {
        if (_documents.Active is { } document)
        {
            document.Output = null;
        }

        ShowOutput();
    }

    /// <summary>Shows the active document's last run, if it has one.</summary>
    /// <remarks>
    /// The headline is the answer, so it goes in the header rather than in a row: a first row that says <c>Accept</c>
    /// would read as one more step of the trace, and the trace is exactly what must stay comparable across runs.
    /// </remarks>
    private void ShowOutput()
    {
        var report = _documents.Active?.Output;

        RunOutput.ItemsSource = report?.Rows;
        RunOutput.SelectedItem = null;
        OutputPanel.IsVisible = report is not null;
        OutputHeader.Text = report is null ? "Output" : $"Output - {report.Headline}";
    }

    /// <summary>Puts the caret on the node a trace line came from.</summary>
    /// <remarks>
    /// The pick is cleared afterwards, so the same row is clickable twice: a trace is something a user reads back
    /// and forth between the document and the panel, unlike a problem, which is a state they are working through.
    /// </remarks>
    private void OnRunRowChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (RunOutput.SelectedItem is not RunRow { Place: { } place }
            || _documents.Active is not { } document
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
            tab.Header = document.IsDirty ? $"{document.Title} *" : document.Title;
        }

        var position = _documents.Active is not { } active
            ? "No document open"
            : $"{active.Title}{FormatFile(active.FilePath)} | {(_documents.AnyUnsaved ? "unsaved changes" : "saved")} | {ThemeText()} | {_fontSize:0} px";

        // The message goes last and is not cleared here: it is the answer to the command the user just
        // ran, and the next command clears it, so it cannot go stale while the user reads it.
        StatusText.Text = _error is null ? position : $"{position} | {_error}";

        UpdateRepresentations();
        UpdateRunCommand();
        UpdateGenerators();
        UpdateExportCommand();
    }

    /// <summary>Says whether the document in front can be run, and why not when it cannot.</summary>
    /// <remarks>
    /// A disabled entry states a fact the user cannot otherwise see - that the language in front defines no
    /// interpreter - so the reason goes on the tooltip as well as into the status bar when F5 is pressed: the command
    /// is refused rather than offered, and "why" must have an answer somewhere.
    /// </remarks>
    private void UpdateRunCommand()
    {
        var runnable = _documents.Active?.Evaluation is not null;

        RunDocumentItem.IsEnabled = runnable;
        ClearOutputItem.IsEnabled = _documents.Active?.Output is not null;
        ToolTip.SetTip(
            RunDocumentItem,
            runnable
                ? "Runs the document with its language's interpreter."
                : "This document's language defines no interpreter.");
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

        if (!_language.TryAdd(language, out error))
        {
            _error = error;
            UpdateStatus();

            return;
        }

        // Every definition document of the folder opens, because that is where the user edits the language: one
        // concept or enumeration per file, one notation per file, one rule or named type per file.
        foreach (var folder in new[] { LanguageProject.StructureFolder, LanguageProject.EditorFolder, LanguageProject.TypesFolder, LanguageProject.ConstraintsFolder })
        {
            var path = Path.Combine(directory, folder);

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

        UpdateStatus();
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

        OpenDocument(model, _language.Notations, typing: WorkbenchLanguage.Typing, types: opened.Types, constraints: opened.Constraints);
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

        TryOpenPath(path);
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
