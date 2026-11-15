using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Avalonia;

/// <summary>
/// The editor as a component: a control that paints one <see cref="EditorSession"/> and scrolls it.
/// </summary>
/// <remarks>
/// <para>
/// This type is deliberately thin. Everything that decides *what* is on screen - which cells are
/// visible, which characters are highlighted, where the caret bar lands - lives in
/// <see cref="EditorFrame"/>, which has no window and is tested with a monospace measure. The control
/// owns only what cannot be tested without a renderer: shaping glyphs, converting layout units to
/// device pixels, and asking the frame what to paint.
/// </para>
/// <para>
/// Input is split the same way. A pointer event becomes a caret with <see cref="HitTest"/>, a key
/// becomes an edit with <see cref="EditorKeyMap"/> and <see cref="KeyRouter"/>, and both are applied
/// to the session as the actions they are. The control never edits the model itself, so every change
/// it makes is undoable and repeatable in exactly the way the editing layer's tests describe.
/// </para>
/// <para>
/// It draws the whole frame itself rather than materialising a child control per cell. A projectional
/// model has thousands of cells and a pane of text is one thing to draw, so the control is a single
/// surface: no control tree, no per-cell measuring, no per-cell invalidation. Virtualization is the
/// viewport's filter, not a recycling scheme.
/// </para>
/// <para>
/// Colour is the one thing the control decides for itself, and even that it delegates. The frame says
/// what each run means, <see cref="Styler"/> can overrule the meaning, and <see cref="Palette"/> says
/// what each meaning looks like - so highlighting is a property of the host rather than of the language,
/// and swapping a palette repaints without re-projecting anything.
/// </para>
/// <para>
/// The control does not own the session. A host may open one session per document, close it when the
/// document does, and keep the same control - so disposing is the host's decision, and
/// <see cref="EditorSession.Dispose"/> is never called from here.
/// </para>
/// </remarks>
public sealed class ProjectionalEditorControl : Control
{
    /// <summary>How wide the caret bar is drawn, in device pixels.</summary>
    /// <remarks>
    /// The caret is a zero-width position in the layout, so the renderer has to give it a thickness of
    /// its own. The value is the control's, not the layout's, which is why it lives here.
    /// </remarks>
    public const double CaretThickness = 1.5;

    /// <summary>How far one wheel notch scrolls, in lines.</summary>
    private const double WheelLines = 3;

    /// <summary>How much blank separates a row number from the edge of the gutter, in device pixels.</summary>
    private const double GutterGap = 6;

    private SyntaxPalette _palette = SyntaxPalette.Light;
    private ICellStyler _styler = CellStyler.Default;

    private double _offsetX;
    private double _offsetY;

    // Where the press that started a drag landed. Held here rather than in the session because it is
    // the mouse's memory of the gesture, not the document's.
    private CaretPosition? _dragAnchor;

    private bool _dragging;

    private readonly CaretBlink _blink = new();

    // The tick that makes the caret blink. Created on attach so a control that is never shown costs no
    // timer, and stopped on detach because a hidden control has nothing to animate.
    private DispatcherTimer? _blinkTimer;

    // The menu that is open, if any. Held so a second right-click closes the first instead of stacking
    // a popup on a popup; the entries themselves are built per opening and never cached.
    private ContextMenu? _menu;

    // The completion list is painted onto this control's own surface rather than into a popup of its
    // own, so it needs a painter and nothing else - no window, no second control, nothing to detach.
    private readonly CompletionPresenter _completion;

    private CompletionPalette _listPalette = CompletionPalette.Light;

    // The rows the gutter numbers, kept between paints because counting them walks the whole document
    // rather than the visible part of it. The layout is a new object after every rebuild, so its identity
    // is the invalidation: a different layout or a different measure means the numbers are stale.
    private IReadOnlyList<LineNumberRow>? _rows;
    private CellLayout? _rowsOfLayout;
    private ITextMeasure? _rowsOfMeasure;

    private bool _showLineNumbers;
    private IBrush _lineNumberBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));
    private IBrush? _gutterBackground;

    /// <summary>Creates a control that renders in the default font at 14 pixels.</summary>
    public ProjectionalEditorControl()
        : this(new Typeface(FontFamily.Default), 14)
    {
    }

    /// <summary>Creates a control that renders in one font at one size.</summary>
    /// <param name="typeface">The font cells are shaped with.</param>
    /// <param name="fontSize">The em size in device pixels.</param>
    public ProjectionalEditorControl(Typeface typeface, double fontSize)
    {
        TextMeasure = new AvaloniaTextMeasure(typeface, fontSize, _palette.BrushOf(TextStyleKind.Default));
        _completion = new CompletionPresenter(TextMeasure, _listPalette);
        Clipboard = new AvaloniaClipboard(() => TopLevel.GetTopLevel(this)?.Clipboard);

        Background = Brushes.White;
        HighlightBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x26, 0x8B, 0xD2));
        CaretBrush = Brushes.Black;

        Focusable = true;
        ClipToBounds = true;

        // The whole surface is text the user can type into, so the pointer says so everywhere on it -
        // the arrow would read as "this is a panel you look at" rather than "this is where you write".
        Cursor = new Cursor(StandardCursorType.Ibeam);
    }

    /// <summary>
    /// The measure that turns text into distances for this control.
    /// </summary>
    /// <remarks>
    /// A session must be opened with this measure, or the layout will be in units that are not pixels
    /// and the text will be drawn at the wrong width. <see cref="OpenSession"/> does that; a session
    /// built elsewhere has to be given it explicitly.
    /// </remarks>
    public AvaloniaTextMeasure TextMeasure { get; }

    /// <summary>The document being edited, or <see langword="null"/> when the control is empty.</summary>
    public EditorSession? Session
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            _offsetX = 0;
            _offsetY = 0;
            _blink.Reset();

            InvalidateMeasure();
            ScrollToCaret();
            InvalidateVisual();
        }
    }

    /// <summary>What is painted behind the text; nothing when <see langword="null"/>.</summary>
    public IBrush? Background { get; set; }

    /// <summary>The colours text is painted in.</summary>
    /// <remarks>
    /// A palette names text colours only; the background, the highlight and the caret stay the host's, so
    /// a dark palette is a dark palette plus a dark <see cref="Background"/>. Changing it repaints without
    /// re-projecting anything, because the meaning of a run is decided before the palette is consulted.
    /// </remarks>
    public SyntaxPalette Palette
    {
        get => _palette;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_palette, value))
            {
                return;
            }

            _palette = value;

            // Shaping in the palette's default colour lets the measuring cache answer for the runs that
            // would be painted in it anyway, instead of shaping those cells a second time.
            TextMeasure.Foreground = value.BrushOf(TextStyleKind.Default);

            InvalidateVisual();
        }
    }

    /// <summary>What decides the meaning of each run, and so which of the palette's colours it gets.</summary>
    /// <remarks>
    /// A language can install its own styler to say more than the default vocabulary does, and a host can
    /// install one to restyle a document without touching its language.
    /// </remarks>
    public ICellStyler Styler
    {
        get => _styler;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _styler = value;
            InvalidateVisual();
        }
    }

    /// <summary>The colour selected text is painted over.</summary>
    public IBrush HighlightBrush { get; set; }

    /// <summary>What the completion list and its icons look like.</summary>
    /// <remarks>
    /// Named for the list rather than for the palette type, because a member named after a type shadows
    /// it inside this class - and because the list is what a host actually sees. The icons are
    /// <see cref="IImage"/>s, so a host can supply its own artwork (a <c>Bitmap</c> from a file works)
    /// instead of the vector markers the presets declare.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public CompletionPalette ListPalette
    {
        get => _listPalette;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_listPalette, value))
            {
                return;
            }

            _listPalette = value;
            _completion.Palette = value;

            InvalidateVisual();
        }
    }

    /// <summary>The colour of the caret bar.</summary>
    public IBrush CaretBrush { get; set; }

    /// <summary>Whether the rows of the document are numbered down the left edge.</summary>
    /// <remarks>
    /// <para>
    /// Off by default, because a projectional document is read as a shape rather than as lines and the
    /// gutter costs horizontal room. A host that wants to talk about "row 12" - a problems panel, a
    /// marker list, a diff - turns it on.
    /// </para>
    /// <para>
    /// Turning it on shortens the room a text cell has to wrap at by <see cref="GutterWidth"/>, so it
    /// re-measures and re-lays-out rather than merely repainting.
    /// </para>
    /// </remarks>
    public bool ShowLineNumbers
    {
        get => _showLineNumbers;

        set
        {
            if (_showLineNumbers == value)
            {
                return;
            }

            _showLineNumbers = value;

            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The colour the row numbers are painted in.</summary>
    public IBrush LineNumberBrush
    {
        get => _lineNumberBrush;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_lineNumberBrush, value))
            {
                return;
            }

            _lineNumberBrush = value;
            InvalidateVisual();
        }
    }

    /// <summary>The colour the gutter is painted over, or <see langword="null"/> to leave the background showing.</summary>
    /// <remarks>
    /// Nothing is painted when this is <see langword="null"/>, which is what a host wants when the
    /// document already sits on a surface of its own: a second shade for the gutter alone would read as a
    /// panel that is not part of the page.
    /// </remarks>
    public IBrush? GutterBackground
    {
        get => _gutterBackground;
        set
        {
            if (ReferenceEquals(_gutterBackground, value))
            {
                return;
            }

            _gutterBackground = value;
            InvalidateVisual();
        }
    }

    /// <summary>How much room the row numbers take on the left edge, in device pixels.</summary>
    /// <remarks>
    /// The width is the widest number the document needs plus the blank that separates it from the text,
    /// so it grows by one digit when the document reaches a row count with one more digit in it. Text is
    /// laid out to the right of the gutter and the gutter itself does not scroll with the document, the
    /// way a code editor's does not.
    /// </remarks>
    public double GutterWidth => _showLineNumbers
        ? (LineNumbers.DigitsOf(Rows) * TextMeasure.Measure("0", TextEmphasis.None)) + (2 * GutterGap)
        : 0;

    /// <summary>The numbered rows of the document as last laid out.</summary>
    /// <remarks>
    /// Public because a host that shows a problems panel wants to say where something is, and the only
    /// honest answer in a projectional document is the row the reader can see. The numbers are positions,
    /// not identities: they change when the document above them changes height.
    /// </remarks>
    public IReadOnlyList<LineNumberRow> LineRows => Rows;

    /// <summary>The clipboard that copy, cut and paste read and write.</summary>
    /// <remarks>
    /// <para>
    /// Defaults to the window's platform clipboard, cached by <see cref="AvaloniaClipboard"/> because a
    /// key press is synchronous and a platform clipboard is not. A host that wants a private clipboard, or
    /// that already has a clipboard service, assigns its own <see cref="ITextClipboard"/>.
    /// </para>
    /// <para>
    /// The cache is refreshed exactly where the platform may have changed since the last look: on gaining
    /// the focus, on a click into the control (a returning window keeps the logical focus, so focus alone
    /// would miss a copy made elsewhere), and when the context menu opens, whose entries run with no chance
    /// to await. The paste key refreshes once more immediately before applying, so no path is stale.
    /// </para>
    /// <para>
    /// A session takes its clipboard when it is created, so assigning this after
    /// <see cref="OpenSession"/> affects the next session rather than the current one.
    /// </para>
    /// </remarks>
    public ITextClipboard Clipboard { get; set; }

    /// <summary>The horizontal scroll offset, in layout units.</summary>
    public double OffsetX => _offsetX;

    /// <summary>The vertical scroll offset, in layout units.</summary>
    public double OffsetY => _offsetY;

    /// <summary>Opens <paramref name="document"/> for editing and makes it what the control shows.</summary>
    /// <param name="document">The root of the model to edit.</param>
    /// <param name="notations">The notation that projects the document's concepts.</param>
    /// <param name="typing">
    /// What the language says about text typed into an empty slot, so that the first keystroke can create
    /// the node it belongs to. Omitting it leaves such a keystroke ignored, which is the behaviour a
    /// language that declares no rules keeps.
    /// </param>
    /// <returns>The session, which the caller owns and must dispose.</returns>
    public EditorSession OpenSession(Node document, NotationRegistry notations, ITypingRules? typing = null)
    {
        var session = new EditorSession(document, notations, TextMeasure, clipboard: Clipboard, typing: typing);
        Session = session;

        return session;
    }

    /// <summary>Re-projects the document after a change this control did not make.</summary>
    public void Refresh()
    {
        if (Session is null)
        {
            return;
        }

        Session.Refresh();

        InvalidateMeasure();
        ScrollToCaret();
        InvalidateVisual();
    }

    /// <summary>Scrolls so the caret is visible, as far as that needs to move.</summary>
    public void ScrollToCaret()
    {
        var session = Session;

        if (session is null)
        {
            return;
        }

        var (x, y) = CaretScroll.Offset(
            session.Layout,
            ViewportSize,
            _offsetX,
            _offsetY,
            session.Selection.Caret,
            session.Measure);

        ScrollTo(x, y);
    }

    /// <summary>Scrolls to an offset, trimmed to the range the document actually has.</summary>
    public void ScrollTo(double x, double y)
    {
        var session = Session;

        if (session is null)
        {
            return;
        }

        var (clampedX, clampedY) = LayoutViewport.Clamp(session.Layout, ViewportSize, x, y);

        if (clampedX == _offsetX && clampedY == _offsetY)
        {
            return;
        }

        _offsetX = clampedX;
        _offsetY = clampedY;

        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var session = Session;

        if (session is null)
        {
            return;
        }

        var viewport = ViewportSize;

        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        // Clamping here rather than in ArrangeOverride keeps a resize from invalidating the visual from
        // inside its own layout pass: the offsets catch up on the way out instead.
        (_offsetX, _offsetY) = LayoutViewport.Clamp(session.Layout, viewport, _offsetX, _offsetY);

        var frame = EditorFrame.Build(
            session.Layout,
            viewport,
            _offsetX,
            _offsetY,
            session.Selection,
            session.Measure,
            Styler);

        if (Background is { } background)
        {
            context.FillRectangle(background, new Rect(Bounds.Size));
        }

        PaintGutter(context);

        foreach (var highlight in frame.Highlights)
        {
            context.FillRectangle(HighlightBrush, ToControl(frame, highlight));
        }

        foreach (var run in frame.TextRuns)
        {
            var bounds = ToControl(frame, run.Bounds);

            context.DrawText(
                TextMeasure.Shaped(run.Text, _palette.BrushOf(run.Style), run.Emphasis),
                new Point(bounds.X, bounds.Y));
        }

        if (frame.CaretRect is { } caret && _blink.IsVisible)
        {
            var bounds = ToControl(frame, caret);

            context.FillRectangle(CaretBrush, new Rect(bounds.X, bounds.Y, CaretThickness, bounds.Height));
        }

        // Last, so the list is never buried by the text it belongs to - it describes what the caret is
        // about to create, so it has to read as an overlay rather than as part of the document.
        if (session.Completion is { IsEmpty: false } list)
        {
            _completion.Render(context, list, CompletionBounds(session, list));
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _blinkTimer = new DispatcherTimer { Interval = CaretBlink.Interval };
        _blinkTimer.Tick += OnBlinkTick;
        _blinkTimer.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_blinkTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnBlinkTick;
            _blinkTimer = null;
        }

        // A popup outlives the visual tree it was opened from, so a control that is detached (closed
        // tab, disposed window) has to take its menu with it.
        _menu?.Close();
        _menu = null;

        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The measure pass is where the editor learns how much room it has, so it publishes the width it is
    /// given as the page a text cell wraps at. An unbounded width means there is no page: the document is
    /// laid out one long line at a time, which is what a host inside a scrolling panel asks for.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var session = Session;

        if (session is not null && !double.IsInfinity(availableSize.Width))
        {
            // The gutter is the host's chrome, not part of the page, so the room a text cell may fill is
            // what is left of the width once the numbers have taken theirs.
            session.WrapWidth = Math.Max(0, availableSize.Width - GutterWidth);
        }

        var content = session?.Layout.Size;

        // A control that fills its parent: the viewport is whatever the host gives it, and a document
        // shorter than the window still occupies the window so clicks below it land somewhere.
        return new Size(
            double.IsInfinity(availableSize.Width) ? content?.Width ?? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? content?.Height ?? 0 : availableSize.Height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Focus is one of the two moments the platform clipboard is read (the other is just before a paste),
    /// because that is when a value copied in another window is most likely to be wanted next.
    /// </remarks>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnGotFocus(e);

        _ = Clipboard.RefreshAsync();
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerWheelChanged(e);

        if (Session is null)
        {
            return;
        }

        var step = TextMeasure.LineHeight * WheelLines;

        ScrollTo(_offsetX - (e.Delta.X * step), _offsetY - (e.Delta.Y * step));

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerPressed(e);

        var session = Session;

        if (session is null)
        {
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsRightButtonPressed)
        {
            OpenContextMenu(session, e);
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();

        // A click into a window that is already focused changes no focus, so this is the second place the
        // clipboard is read - otherwise a copy made in another window would never be noticed.
        _ = Clipboard.RefreshAsync();

        // The list overlays the document, so it gets the first refusal: a click on a proposal must not
        // also move the caret into the slot the proposal belongs to.
        if (ClickCompletion(session, e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        if (CaretAt(e) is not { } position)
        {
            return;
        }

        if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            // Shift extends from the end that is already anchored; a plain click would have dropped it.
            session.Select(new Selection(session.Selection.Anchor, position));
        }
        else
        {
            session.Select(Selection.At(position));

            _dragAnchor = position;
            _dragging = true;

            e.Pointer.Capture(this);
        }

        Show();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerMoved(e);

        var session = Session;

        if (session is null || !_dragging || _dragAnchor is not { } anchor)
        {
            return;
        }

        if (CaretAt(e) is not { } position || position.Equals(session.Selection.Caret))
        {
            return;
        }

        session.Select(new Selection(anchor, position));
        Show();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerReleased(e);

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _dragAnchor = null;

        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnKeyDown(e);

        var session = Session;

        if (session is null)
        {
            return;
        }

        if (Shortcut(session, e))
        {
            e.Handled = true;

            return;
        }

        if (EditorKeyMap.Map(e.Key, e.KeyModifiers) is not { } key)
        {
            return;
        }

        var action = KeyRouter.For(session, key);

        if (key == EditorKey.Paste)
        {
            // The platform clipboard may have been filled by another application while this control kept
            // the focus, so the read has to happen before the edit - and a read is asynchronous.
            _ = PasteAsync(session, action);
        }
        else
        {
            AfterEdit(session.Apply(action));
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnTextInput(e);

        var session = Session;

        if (session is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var result = session.Apply(KeyRouter.Type(session, e.Text));

        // A refused character is left unconsumed, so a host can react to it - a beep, a status line.
        e.Handled = result.Outcome != EditOutcome.Rejected;

        AfterEdit(result);
    }

    private bool Shortcut(EditorSession session, KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || e.Key is not (Key.Z or Key.Y))
        {
            return false;
        }

        var undo = e.Key == Key.Z && (e.KeyModifiers & KeyModifiers.Shift) == 0;

        if (!(undo ? session.Undo() : session.Redo()))
        {
            return false;
        }

        Show();

        return true;
    }

    /// <summary>Waits for the platform clipboard and then pastes what it holds.</summary>
    /// <remarks>
    /// The wait is the cost of a clipboard shared with other processes: the text this application last saw
    /// is a cache, and a copy made in another window would otherwise be pasted one press late. The edit is
    /// applied when the read answers, so it lands where the caret is then - which is the caret the user is
    /// looking at, because the control had the focus the whole time.
    /// </remarks>
    private async Task PasteAsync(EditorSession session, EditAction action)
    {
        await Clipboard.RefreshAsync().ConfigureAwait(true);

        AfterEdit(session.Apply(action));
    }

    private void AfterEdit(EditResult result)
    {
        if (result.Changed)
        {
            // The document grew or shrank, so the space it wants changed as well.
            InvalidateMeasure();
        }

        Show();
    }

    /// <summary>Opens the context menu for the caret as the right button found it.</summary>
    /// <remarks>
    /// The caret follows the click unless the click landed inside an existing selection, because
    /// dragging a range and then right-clicking it must keep the range - that is what every editor does
    /// and what makes "delete this" possible. The entries are built now rather than when the menu is
    /// first needed, so what the user sees is the state the click produced.
    /// </remarks>
    private void OpenContextMenu(EditorSession session, PointerPressedEventArgs e)
    {
        Focus();

        // The entries are built and run synchronously, so the paste entry needs the cache current before
        // the menu exists rather than when it is clicked.
        _ = Clipboard.RefreshAsync();

        if (CaretAt(e) is { } position)
        {
            MoveCaretUnlessSelected(session, position);
        }

        _menu?.Close();
        _menu = EditorMenuPresenter.Menu(EditorMenu.For(session), AfterMenuCommand);
        _menu.Open(this);

        e.Handled = true;
    }

    private void MoveCaretUnlessSelected(EditorSession session, CaretPosition position)
    {
        var selection = session.Selection;
        var inside = selection.HasText
            && selection.Caret.Address.Equals(position.Address)
            && position.Offset >= selection.Start
            && position.Offset <= selection.End;

        if (!inside)
        {
            session.Select(Selection.At(position));
            Show();
        }
    }

    private void AfterMenuCommand()
    {
        // A command is a command however the user picked it, so the menu goes away here rather than
        // waiting for the toolkit: a click would close it, but a click is not the only way in.
        _menu?.Close();
        _menu = null;

        // A command runs the session directly, so no EditResult comes back to say whether the document
        // changed size. Measuring again is the safe answer and the cheap one: it costs nothing visible
        // and it is what an inserted node needs.
        InvalidateMeasure();
        Show();
    }

    private void Show()
    {
        _blink.Reset();
        ScrollToCaret();
        InvalidateVisual();
    }

    private void OnBlinkTick(object? sender, EventArgs e)
    {
        // The caret blinks only while the editor has the focus and stands inside something typeable. A
        // control that lost the focus keeps a steady caret, which is what every platform does: a blinking
        // caret in a window the user is not working in is noise.
        var blinking = IsFocused
            && Session is { } session
            && CaretBlink.Blinks(session.Tree, session.Selection.Caret);

        if (_blink.Advance(blinking))
        {
            InvalidateVisual();
        }
    }

    private CaretPosition? CaretAt(PointerEventArgs e)
    {
        var session = Session;

        if (session is null)
        {
            return null;
        }

        var point = e.GetPosition(this);

        // The gutter is chrome outside the page, so a press in it is not a press into the document.
        if (point.X < GutterWidth)
        {
            return null;
        }

        // Pointer coordinates are device pixels; the layout's are its own units, shifted by the scroll.
        return HitTest.Caret(
            session.Tree,
            session.Layout,
            point.X - GutterWidth + _offsetX,
            point.Y + _offsetY,
            session.Measure);
    }

    private LayoutSize ViewportSize => new(Math.Max(0, Bounds.Width - GutterWidth), Bounds.Height);

    /// <summary>Where the completion list is drawn, in viewport coordinates.</summary>
    /// <remarks>
    /// Below the caret and shifted right of it, flipped above when it would hang off the bottom edge,
    /// then clamped so it never leaves the surface. Keeping it inside the control rather than over the
    /// host's other content is the reason the list is painted here instead of in a popup.
    /// </remarks>
    private Rect CompletionBounds(EditorSession session, CompletionList list)
    {
        var size = _completion.Measure(list);

        var caret = CaretBounds.Of(session.Tree, session.Layout, session.Selection.Caret, session.Measure);

        var left = (caret?.X ?? 0) - _offsetX + GutterWidth;
        var top = (caret is { } below ? below.Y - _offsetY + below.Height : 0) + _completion.RowHeight;

        if (caret is { } flip && top + size.Height > Bounds.Height)
        {
            top = flip.Y - _offsetY - size.Height;
        }

        return new Rect(
            Math.Clamp(left, 0, Math.Max(0, Bounds.Width - size.Width)),
            Math.Clamp(top, 0, Math.Max(0, Bounds.Height - size.Height)),
            size.Width,
            size.Height);
    }

    /// <summary>Accepts the row of the completion list the pointer pressed, if it hit one.</summary>
    /// <returns>Whether the press was the list's rather than the document's.</returns>
    /// <remarks>
    /// A click is turned into the two actions the keyboard runs - move the highlight, then accept - so a
    /// row cannot be accepted through a path that the keyboard would have refused.
    /// </remarks>
    private bool ClickCompletion(EditorSession session, Point point)
    {
        if (session.Completion is not { IsEmpty: false } list
            || _completion.RowAt(list, CompletionBounds(session, list), point) is not { } index)
        {
            return false;
        }

        var delta = index - list.SelectedIndex;

        if (delta != 0)
        {
            session.Apply(new SelectCompletionAction(delta));
        }

        AfterEdit(session.Apply(new AcceptCompletionAction()));

        return true;
    }

    private static Rect ToRect(LayoutRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>A layout rectangle in this control's coordinates: through the scroll, then past the gutter.</summary>
    private Rect ToControl(EditorFrame frame, LayoutRect rect)
    {
        var bounds = frame.Viewport.ToViewport(rect);

        return new Rect(bounds.X + GutterWidth, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>The numbered rows of the document, counted at most once per layout.</summary>
    /// <remarks>
    /// Counting walks the whole document, so it must not happen on every repaint - the caret blinks, and a
    /// repaint per blink must not cost a pass over every cell. <see cref="CellLayout"/> is a fresh object
    /// after every rebuild, so its identity answers "is this the layout the numbers were counted from".
    /// </remarks>
    private IReadOnlyList<LineNumberRow> Rows
    {
        get
        {
            var session = Session;

            if (session is null)
            {
                return [];
            }

            if (_rows is null
                || !ReferenceEquals(_rowsOfLayout, session.Layout)
                || !ReferenceEquals(_rowsOfMeasure, session.Measure))
            {
                _rows = LineNumbers.Of(session.Layout, session.Measure);
                _rowsOfLayout = session.Layout;
                _rowsOfMeasure = session.Measure;
            }

            return _rows;
        }
    }

    /// <summary>Paints the row numbers down the left edge, outside the scroll.</summary>
    /// <remarks>
    /// The gutter does not scroll with the document - a row number that slid away with the page would stop
    /// being a reference the reader can use - so the numbers are placed from the layout's own heights minus
    /// the scroll offset, and only the rows that are on screen are shaped.
    /// </remarks>
    private void PaintGutter(DrawingContext context)
    {
        var width = GutterWidth;

        if (width <= 0)
        {
            return;
        }

        if (GutterBackground is { } background)
        {
            context.FillRectangle(background, new Rect(0, 0, width, Bounds.Height));
        }

        var right = width - GutterGap;
        var height = Bounds.Height;

        foreach (var row in Rows)
        {
            var top = row.Bounds.Y - _offsetY;

            if (top + row.Bounds.Height <= 0 || top >= height)
            {
                continue;
            }

            var text = TextMeasure.Shaped(
                row.Number.ToString(CultureInfo.InvariantCulture),
                LineNumberBrush,
                TextEmphasis.None);

            context.DrawText(text, new Point(right - text.WidthIncludingTrailingWhitespace, top));
        }
    }
}
