using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using Projectional.Core;
using Projectional.Editing;
using Projectional.Persistence;
using Projectional.Projection;

using Projectional.Web;
using Projectional.Web.App.Storage;

namespace Projectional.Web.App.Shell;

/// <summary>
/// The browser shell: a menu bar, document tabs, the painted editor, a problems panel and a status bar.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole frontend, and it owns nothing the framework already owns. The projection, the caret, the
/// selection and the undo history are <see cref="EditorSession"/>'s; the keys are <see cref="WebKeyMap"/>'s; the
/// pixels are <see cref="EditorMarkup"/>'s. What is left here is the part a browser genuinely adds - a menu, a tab
/// strip, the input element, JavaScript callbacks and storage - which is why the file is long but shallow.
/// </para>
/// <para>
/// The editor is a projectional canvas, not a text area: the hidden <c>textarea</c> exists only so the operating
/// system's input method and the clipboard have somewhere to deliver characters. Every key that means something to
/// the editor is swallowed before the browser sees it and routed through the same <see cref="KeyRouter"/> the
/// desktop control uses.
/// </para>
/// </remarks>
public sealed partial class EditorShell : IDisposable
{
    private const double MinimumFontSize = 11;
    private const double MaximumFontSize = 30;

    private EditorWorkspace? _workspace;
    private DotNetObjectReference<EditorShell>? _self;

    private ElementReference _host;
    private ElementReference _composer;

    private EditorFrame? _frame;
    private string _canvas = string.Empty;
    private CellMetrics _metrics = CellMetrics.Default;

    private string? _menu;
    private IReadOnlyList<WebLanguage> _languages = [];
    private bool _languagesLoaded;
    private bool _showProject = true;
    private bool _showProblems = true;
    private bool _dark;
    private bool _dragging;
    private bool _scrollPending;
    private bool _measurePending = true;
    private double _fontSize = 16;
    private string _message = string.Empty;
    private CaretPosition? _anchor;
    private string _lastPlatformText = string.Empty;
    private readonly HashSet<NodeId> _collapsedOutline = [];

    [Inject]
    private BrowserDocumentStorage Storage { get; set; } = default!;

    [Inject]
    private EditorInterop Interop { get; set; } = default!;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    private EditorDocument? Active => _workspace?.Active;

    private IReadOnlyList<EditorDocument> Documents => _workspace?.Documents ?? [];

    /// <summary>The languages the explorer and the menus offer; empty until the declared samples are read.</summary>
    private IReadOnlyList<WebLanguage> Languages => _languages;

    /// <summary>The model browser of the active document, with the branches the user shut removed.</summary>
    private IReadOnlyList<OutlineRow> OutlineRows =>
        Active is { } document ? DocumentOutline.Of(document.Model, _collapsedOutline) : [];

    /// <summary>The node the caret is in, which is the browser row drawn as current.</summary>
    private NodeId? CaretNode => Active?.CaretNode;

    private CompletionList? Completion => Active?.Session.Completion;

    private string FontSize => _fontSize.ToString("0.#", CultureInfo.InvariantCulture);

    private string RootStyle => string.Create(
        CultureInfo.InvariantCulture,
        $"--pn-font-size:{_fontSize:0.#}px");

    private string CompletionStyle
    {
        get
        {
            if (_frame?.CaretRect is not { } caret)
            {
                return "display:none";
            }

            var left = _metrics.X(caret.X);
            var top = _metrics.Y(caret.Y + Math.Max(caret.Height, 1));

            return string.Create(CultureInfo.InvariantCulture, $"left:{left:0.###}px;top:{top:0.###}px");
        }
    }

    protected override void OnInitialized()
    {
        _workspace = new EditorWorkspace(Storage);

        var restored = _workspace.Restore();

        if (restored == 0)
        {
            // A first visit shows what the sample is: two languages, each with a document that already says
            // something, rather than an empty page a reader has to fill before the editor means anything.
            _workspace.New(WebLanguages.Rules, sample: true);
            _workspace.New(WebLanguages.StateMachine, sample: true);
            _workspace.Activate(_workspace.Documents[0]);
            _workspace.Save();
            _message = "Two examples are open. Everything you type is kept in this browser.";
        }
        else
        {
            _message = $"{restored} document(s) restored from this browser.";
        }

        Refresh();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _self = DotNetObjectReference.Create(this);

            await Interop.AttachAsync(_composer, _self, [.. WebKeyMap.HandledChords]);
        }

        if (_measurePending && Active is not null)
        {
            _measurePending = false;

            var metrics = await Interop.MeasureAsync(_host);

            if (metrics != _metrics)
            {
                _metrics = metrics;
                Refresh();

                return;
            }
        }

        if (_scrollPending)
        {
            _scrollPending = false;

            await Interop.ScrollCaretIntoViewAsync(_host);
        }

        if (!_languagesLoaded)
        {
            _languagesLoaded = true;

            // After the first paint on purpose: reading the declared samples touches the file system, and the shell
            // should appear before that cost is paid. The four code-declared languages are already usable.
            _languages = WebLanguages.All;

            StateHasChanged();
        }
    }

    /// <summary>Routes a key the page swallowed, and answers whether the editor used it.</summary>
    /// <param name="key">The browser's <c>KeyboardEvent.key</c>.</param>
    /// <param name="control">Whether Control was held.</param>
    /// <param name="shift">Whether Shift was held.</param>
    /// <param name="alt">Whether Alt was held.</param>
    [JSInvokable]
    public Task<bool> OnKeyDown(string key, bool control, bool shift, bool alt)
    {
        _menu = null;

        if (Active is not { } document)
        {
            return Task.FromResult(false);
        }

        var letter = key.ToLowerInvariant();

        // The shell's own gestures: the page asked the key map whether to swallow them, so here they are.
        if (control && !alt)
        {
            switch (letter)
            {
                case "s" when !shift:
                    Save();

                    return Task.FromResult(true);

                case "n" when !shift:
                    NewDocument(document.Language, sample: false);

                    return Task.FromResult(true);

                case "z" when !shift:
                    document.Undo();
                    AfterChange();

                    return Task.FromResult(true);

                case "y" when !shift:
                case "z" when shift:
                    document.Redo();
                    AfterChange();

                    return Task.FromResult(true);
            }
        }

        if (WebKeyMap.Map(key, control, shift, alt) is not { } editorKey)
        {
            return Task.FromResult(false);
        }

        ApplyKey(document, editorKey);

        return Task.FromResult(true);
    }

    /// <summary>Applies one editor key and keeps the two consequences every gesture shares in one place.</summary>
    /// <remarks>
    /// The keyboard path and the menu path both come through here, because the clipboard is the case where doing it
    /// twice would be invisible: a copy that reached the model but not the operating system looks like it worked
    /// until the user leaves the page.
    /// </remarks>
    private void ApplyKey(EditorDocument document, EditorKey key)
    {
        document.Press(key);
        AfterChange();

        if (key is EditorKey.Copy or EditorKey.Cut && document.Session.Clipboard.Text is { Length: > 0 } text)
        {
            _lastPlatformText = text;

            _ = Interop.WriteClipboardAsync(text);
        }
    }

    /// <summary>Feeds the characters a text field delivered, which is how typing and input methods arrive.</summary>
    /// <param name="text">The characters that were inserted.</param>
    [JSInvokable]
    public void OnTextInput(string text)
    {
        Active?.Type(text);
        AfterChange();
    }

    public void Dispose()
    {
        _self?.Dispose();
        _workspace?.Dispose();

        _ = DisposeInteropAsync();
    }

    private async Task DisposeInteropAsync()
    {
        if (Interop is not null)
        {
            await Interop.DisposeAsync();
        }
    }

    private void Refresh()
    {
        if (Active is { } document)
        {
            _frame = document.Frame();
            _canvas = EditorMarkup.Canvas(_frame, document.Extent, _metrics);
        }
        else
        {
            _frame = null;
            _canvas = string.Empty;
        }

        StateHasChanged();
    }

    /// <summary>The one place every edit passes through: autosave, status and scroll, in that order.</summary>
    private void AfterChange()
    {
        _workspace?.PersistActive();

        _message = Active?.LastMessage ?? string.Empty;
        _scrollPending = true;

        Refresh();
    }

    private void OnPointerDown(PointerEventArgs args)
    {
        _menu = null;

        if (Active is not { } document || CaretAt(document, args) is not { } position)
        {
            return;
        }

        var selection = document.Session.Selection;

        if (args.ShiftKey)
        {
            // Shift extends from the end the selection was started at, which is what every editor does.
            document.Session.Select(new Selection(selection.Anchor, position));
            _dragging = false;
        }
        else
        {
            document.Session.Select(Selection.At(position));
            _anchor = position;
            _dragging = true;
        }

        _ = Interop.FocusAsync(_composer);

        Refresh();
    }

    /// <summary>The third click of a triple click selects the whole value.</summary>
    /// <param name="args">The click, whose <see cref="MouseEventArgs.Detail"/> counts the clicks.</param>
    private void OnClick(MouseEventArgs args)
    {
        if (args.Detail < 3 || Active is not { } document)
        {
            return;
        }

        document.SelectValue();
        Refresh();
    }

    /// <summary>A double click selects the word under the pointer.</summary>
    /// <param name="args">The click that completed the double click.</param>
    private void OnDoubleClick(MouseEventArgs args)
    {
        if (Active is not { } document)
        {
            return;
        }

        document.SelectWord();
        Refresh();
    }

    private void OnPointerMove(PointerEventArgs args)
    {
        if (!_dragging || _anchor is not { } anchor || Active is not { } document)
        {
            return;
        }

        if (CaretAt(document, args) is not { } position)
        {
            return;
        }

        document.Session.Select(new Selection(anchor, position));

        Refresh();
    }

    private void OnPointerUp()
    {
        _dragging = false;
        _anchor = null;
    }

    /// <summary>The caret a pointer at <paramref name="args"/> means, in layout units.</summary>
    private CaretPosition? CaretAt(EditorDocument document, PointerEventArgs args) =>
        HitTest.Caret(
            document.Session.Tree,
            document.Session.Layout,
            args.OffsetX / _metrics.CellWidth,
            args.OffsetY / _metrics.CellHeight,
            document.Session.Measure);

    private void ToggleMenu(string name) => _menu = _menu == name ? null : name;

    private void FocusComposer() => _ = Interop.FocusAsync(_composer);

    private void NewRules() => NewDocument(WebLanguages.Rules, sample: false);

    private void NewStateMachine() => NewDocument(WebLanguages.StateMachine, sample: false);

    private void OpenRulesExample() => NewDocument(WebLanguages.Rules, sample: true);

    private void OpenStateMachineExample() => NewDocument(WebLanguages.StateMachine, sample: true);

    private void NewDocument(WebLanguage language, bool sample)
    {
        if (_workspace is null)
        {
            return;
        }

        _menu = null;
        _workspace.New(language, sample);
        _workspace.Persist();

        _message = sample ? $"Opened the {language.Name} example." : $"New {language.Name} document.";
        _measurePending = true;

        Refresh();
        FocusComposer();
    }

    private void Activate(EditorDocument document)
    {
        _menu = null;

        if (_workspace?.Activate(document) == true)
        {
            _message = string.Empty;
            _measurePending = true;

            Refresh();
            FocusComposer();
        }
    }

    private void CloseDocument(EditorDocument document)
    {
        _menu = null;

        if (_workspace?.Close(document) == true)
        {
            _message = $"Closed '{document.Name}'.";
            Refresh();
        }
    }

    private void Save()
    {
        if (_workspace is null)
        {
            return;
        }

        _menu = null;

        var count = _workspace.Save();

        _message = $"Saved {count} document(s) in this browser.";
        Refresh();
    }

    private async Task DownloadActiveAsync()
    {
        if (Active is not { } document)
        {
            return;
        }

        _menu = null;

        await Interop.DownloadAsync(document.Name + ModelFile.Extension, document.Xml);

        _message = $"Downloaded '{document.Name}{ModelFile.Extension}'.";
        Refresh();
    }

    private async Task RenameActiveAsync()
    {
        if (_workspace?.Active is not { } document)
        {
            return;
        }

        _menu = null;

        var name = await Js.InvokeAsync<string?>("prompt", "Document name", document.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _workspace.Rename(document, name);
        _message = $"Renamed to '{document.Name}'.";
        Refresh();
    }

    private async Task OpenFileAsync(InputFileChangeEventArgs args)
    {
        _menu = null;

        if (_workspace is null)
        {
            return;
        }

        try
        {
            using var stream = args.File.OpenReadStream(maxAllowedSize: 4 * 1024 * 1024);
            using var reader = new StreamReader(stream);

            var xml = await reader.ReadToEndAsync();
            var name = Path.GetFileNameWithoutExtension(args.File.Name);

            if (OpenInSomeLanguage(xml, name) is not { } document)
            {
                return;
            }

            _workspace.Persist();

            _message = $"Opened '{args.File.Name}' as {document.Language.Name}.";
            _measurePending = true;
            FocusComposer();
        }
        catch (Exception exception) when (exception is ModelFormatException or IOException or UnauthorizedAccessException)
        {
            _message = $"'{args.File.Name}' could not be opened: {exception.Message}";
        }

        Refresh();
    }

    /// <summary>
    /// Opens <paramref name="xml"/> in the active language, or in the first one that can read it.
    /// </summary>
    /// <remarks>
    /// A stored model names its concepts, not its language, so a browser shell cannot know from the file which
    /// language to open it in. Trying the active one first keeps the common case - the tab you are working in - and
    /// trying the rest afterwards means a document dragged in from another tab still opens instead of being refused
    /// for a reason the file does not contain.
    /// </remarks>
    private EditorDocument? OpenInSomeLanguage(string xml, string name)
    {
        if (_workspace is null)
        {
            return null;
        }

        var active = _workspace.Active?.Language;
        var languages = new List<WebLanguage>();

        if (active is not null)
        {
            languages.Add(active);
        }

        languages.AddRange(WebLanguages.All.Where(language => !ReferenceEquals(language, active)));

        var refusal = string.Empty;

        foreach (var language in languages)
        {
            try
            {
                return _workspace.OpenXml(xml, language, name);
            }
            catch (ModelFormatException exception)
            {
                refusal = exception.Message;
            }
        }

        _message = $"'{name}' could not be opened: {refusal}";

        return null;
    }

    private void PressKey(EditorKey key)
    {
        _menu = null;

        if (Active is { } document)
        {
            ApplyKey(document, key);
        }
    }

    /// <summary>Handles a native paste, which is the only text another application's clipboard exposes here.</summary>
    /// <param name="text">What the browser read from the clipboard.</param>
    /// <remarks>
    /// A structural copy is the more specific payload, so it wins when the pasted text is the text the editor itself
    /// last put on the clipboard - the user copied a node and is pasting that node rather than the sentence behind it.
    /// </remarks>
    [JSInvokable]
    public void OnPaste(string text)
    {
        if (Active is not { } document)
        {
            return;
        }

        var clipboard = document.Session.Clipboard;

        if (clipboard.Fragment is not { IsEmpty: false } || (text.Length > 0 && text != _lastPlatformText))
        {
            clipboard.Text = text;
        }

        document.Press(EditorKey.Paste);
        AfterChange();
    }

    private void AcceptCompletion(int index)
    {
        Active?.AcceptCompletion(index);
        AfterChange();
        FocusComposer();
    }

    private void GoTo(ProblemRow row)
    {
        Active?.Select(row.Address);
        _scrollPending = true;

        Refresh();
        FocusComposer();
    }

    private void Zoom(double delta)
    {
        _fontSize = Math.Clamp(_fontSize + delta, MinimumFontSize, MaximumFontSize);
        _measurePending = true;
        _menu = null;

        Refresh();
    }

    private void ResetZoom()
    {
        _fontSize = 16;
        _measurePending = true;
        _menu = null;

        Refresh();
    }

    private void ToggleProject()
    {
        _showProject = !_showProject;
        _menu = null;

        Refresh();
    }

    private void ToggleOutlineRow(NodeId id, bool expanded)
    {
        if (expanded)
        {
            _collapsedOutline.Add(id);
        }
        else
        {
            _collapsedOutline.Remove(id);
        }

        Refresh();
    }

    private void ExpandOutline()
    {
        _collapsedOutline.Clear();

        Refresh();
    }

    private void CollapseOutline()
    {
        if (Active is { } document)
        {
            _collapsedOutline.Clear();

            foreach (var id in DocumentOutline.NodesWithChildren(document.Model))
            {
                _collapsedOutline.Add(id);
            }
        }

        Refresh();
    }

    /// <summary>Opens every branch above the caret and makes the caret's row the one in view.</summary>
    private void RevealCaret()
    {
        if (Active is not { } document || document.CaretNode is not { } id)
        {
            return;
        }

        foreach (var ancestor in DocumentOutline.AncestorsOf(document.Model, id))
        {
            _collapsedOutline.Remove(ancestor);
        }

        _scrollPending = true;

        Refresh();
    }

    private void GoToNode(NodeId id)
    {
        Active?.SelectNode(id);

        _scrollPending = true;

        Refresh();
        FocusComposer();
    }

    private void ToggleProblems()
    {
        _showProblems = !_showProblems;
        _menu = null;

        Refresh();
    }

    private void ToggleTheme()
    {
        _dark = !_dark;
        _menu = null;

        Refresh();
    }

    private void UndoActive()
    {
        _menu = null;
        Active?.Undo();
        AfterChange();
    }

    private void RedoActive()
    {
        _menu = null;
        Active?.Redo();
        AfterChange();
    }

    private void CloseActive()
    {
        if (Active is { } document)
        {
            CloseDocument(document);
        }
    }
}
