using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
/// <para>
/// It is a <see cref="ILogicalScrollable"/> so a host can put it in a <see cref="ScrollViewer"/> and get
/// the platform's bars, without the editor owning a bar, a wrapper or a second offset. The protocol is
/// spoken in layout units - the step is one cell, the page keeps a line of overlap - which is why the
/// bars move by whole rows rather than by pixels.
/// </para>
/// </remarks>
public sealed class ProjectionalEditorControl : Control, ILogicalScrollable
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

    /// <summary>How much blank separates the whole margin from the first column of text, in device pixels.</summary>
    /// <remarks>
    /// Two pixels, and it is what makes a row number read as a label beside the document rather than as the
    /// first thing on the line. It is added once, at the end, so it separates the margin as a whole from
    /// the page - the bulb's own gap and this one are different jobs.
    /// </remarks>
    private const double PageMargin = 2;

    private SyntaxPalette _palette = SyntaxPalette.Light;
    private PreviewPalette _previewPalette = PreviewPalette.Light;
    private GhostFrame? _ghost;
    private GhostPresenter? _ghostPainter;
    private ICellStyler _styler = CellStyler.Default;

    private double _offsetX;
    private double _offsetY;

    // What a scroll host asked for. Recorded rather than obeyed: a host hides its bars with these, and the
    // wheel and the caret keep scrolling either way - a document the caret is in must stay reachable.
    private bool _canScrollHorizontally = true;
    private bool _canScrollVertically = true;

    // A scroll host's only notification that the numbers it reads back have moved. Held as a field because
    // an explicit interface event cannot be raised from the class itself.
    private EventHandler? _scrollInvalidated;

    // The numbers a scroll host was last told. Comparing against them is what makes a resize safe to
    // notice twice: Bounds is a direct property whose change carries no previous value, and the new width
    // only becomes a new extent inside the measure pass, so the geometry is checked before and after the
    // document is re-laid-out and only the check that sees a difference raises.
    private (LayoutSize Extent, LayoutSize Viewport) _reported;

    // Where the press that started a drag landed. Held here rather than in the session because it is
    // the mouse's memory of the gesture, not the document's.
    private CaretPosition? _dragAnchor;

    private bool _dragging;

    // The column boundary the press grabbed, and where the pointer was when it did. Held here rather
    // than in the session because it is the mouse's memory of the gesture, not the document's - the
    // session only ever sees the width the drag has reached.
    private ColumnHandle? _resizing;

    private double _resizeOrigin;

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

    // The offer list is the same arrangement: a painter over this surface, placed beside the caret and
    // sharing the list palette, because both are lists drawn on the document.
    private readonly OfferPresenter _offers;

    /// <summary>How often the preview ladder is asked whether a rung has been reached.</summary>
    /// <remarks>
    /// Thirty-millisecond steps for rungs that are hundreds of milliseconds apart: fine enough that the description
    /// and the ghost appear when they were promised, coarse enough that a hover costs nothing worth measuring.
    /// </remarks>
    private const int PreviewTickMs = 30;

    // What the offer list shows on a long hover: the ladder, the copy an entry's edit was applied to and the ghost
    // of that copy. Built with the session, because the copy is a copy of that session's document, and ticked only
    // while an entry is hovered - a document nobody is pointing at costs no timer.
    private OfferPreview? _preview;
    private DispatcherTimer? _previewTimer;
    private GhostFrame? _previewGhost;

    // The description the ladder's middle rung shows - the sentence an entry carries - wrapped once per width, so
    // a repaint per tick does not measure it again. Keyed on the sentence and the room it was wrapped for.
    private (string Heading, string Description, double Room)? _offerTipOf;
    private IReadOnlyList<string>? _offerTipLines;
    private Size _offerTipSize;

    private CompletionPalette _listPalette = CompletionPalette.Light;

    // The rows the gutter numbers, kept between paints because counting them walks the whole document
    // rather than the visible part of it. The layout is a new object after every rebuild, so its identity
    // is the invalidation: a different layout or a different measure means the numbers are stale.
    private IReadOnlyList<LineNumberRow>? _rows;
    private CellLayout? _rowsOfLayout;
    private ITextMeasure? _rowsOfMeasure;

    // What the last raise described, so a repaint that moved nothing stays silent.
    private Selection? _published;

    private bool _showLineNumbers;
    private IBrush _lineNumberBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));
    private IBrush? _gutterBackground;

    // The bulb, and the answer "does this line offer anything" kept between paints: the caret blinks, so
    // a repaint per tick must not walk the document's findings. The layout is a fresh object after every
    // rebuild and both sets are immutable, so their identity is the invalidation.
    private bool _showBulb = true;
    private IBrush _bulbBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0x87, 0x1C));

    // The fold arrows, kept between paints for the same reason the rows are: finding them walks the whole
    // document and the whole layout, and the caret blinks. The layout is a fresh object after every rebuild
    // and the fold set is immutable, so their identity is the invalidation.
    private bool _showFoldTwisties = true;
    private string _collapsedGlyph = "+";
    private string _expandedGlyph = "-";
    private IReadOnlyList<FoldMark>? _foldMarks;
    private CellLayout? _foldMarksOf;
    private CollapsedNodeSet? _foldMarksSet;
    private bool _bulbOffered;
    private (CellLayout Layout, CaretPosition Caret, MarkerSet Markers, FixSet Fixes)? _bulbOf;

    // The pointer shape this control last asked for, kept as the shape rather than as a Cursor so that a
    // move which does not change it sets no property and allocates nothing. Only a crossing allocates, which
    // is also how the fold arrow's hand behaved before there were two targets.
    private StandardCursorType _pointer = StandardCursorType.Ibeam;

    // The type is MarkerPalette; the property below cannot carry that name, because an instance member
    // shadows a type of the same name everywhere inside this class.
    private MarkerPalette _markerColors = MarkerPalette.Light;

    // The findings as last placed. Geometry walks the document's findings and the whole layout, and the
    // caret blink repaints twice a second, so the result is kept until one of its three inputs is replaced.
    // A MarkerSet is immutable and the layout is a new object after every rebuild, so identity is enough.
    // One list serves both the waves and the tip, so asking where a marker is never walks the document twice.
    private IReadOnlyList<MarkerLine>? _lines;
    private CellLayout? _linesOfLayout;
    private MarkerSet? _linesOfMarkers;
    private ITextMeasure? _linesOfMeasure;

    // The waves, merged from the lines above. Derived state, so its invalidation is the identity of the
    // list it was merged from rather than of the layout it was computed out of.
    private IReadOnlyList<MarkerRun>? _runs;
    private IReadOnlyList<MarkerLine>? _runsOfLines;

    // The tip is painted onto this control's own surface, like the completion list and for the same
    // reason: it stays testable headlessly and can never outlive the control it describes.
    private readonly MarkerTipPresenter _tip;

    private MarkerTipPalette _tipPalette = MarkerTipPalette.Light;

    // The squiggle the pointer is resting on, if any. Hovering is pointer state, not document state: it is
    // never stored in the model, never persisted, and never outlives the pointer leaving the wave.
    private MarkerHit? _hover;

    // The tip's lines and the size its box needs, wrapped at most once per squiggle and text width.
    // Wrapping measures glyphs, and a repaint must not measure again - so the width it was wrapped for is
    // part of the state, and a narrower window re-wraps rather than keeping a box sized for the old room.
    private IReadOnlyList<string>? _tipLines;
    private Size _tipSize;
    private double _tipWidth = -1;
    private string _tipFixes = string.Empty;

    // The concept tip, which is the other tooltip this control can show. It is deliberately a separate
    // state from the marker tip rather than a second kind of it: the marker tip follows a squiggle and is
    // shown at once, while this one follows ordinary text and waits - so the two have different triggers
    // even though they share a presenter and a placement rule.
    private const int ConceptTipDelayMs = 600;

    private NodeId? _hoverNode;
    private string? _hoverSlot;
    private CellAddress? _conceptTipAnchor;
    private DispatcherTimer? _conceptTipTimer;
    private TipContent? _conceptTip;
    private NodeId? _conceptTipNode;
    private IReadOnlyList<string>? _conceptTipLines;
    private Size _conceptTipSize;
    private double _conceptTipWidth = -1;

    /// <summary>How many repairs the session offers for <paramref name="diagnostic"/>.</summary>
    /// <remarks>
    /// Read from the session rather than remembered: the fix set is republished by the host, so the count
    /// the tip shows is always the one that was true when the tip was built.
    /// </remarks>
    private int CountOf(Projectional.Semantics.Diagnostic diagnostic) =>
        Session?.Fixes.For(diagnostic).Count ?? 0;

    /// <summary>The repair counts of <paramref name="diagnostics"/>, as one string to compare with.</summary>
    private string TopicFixes(IReadOnlyList<Projectional.Semantics.Diagnostic> diagnostics) =>
        string.Join(',', diagnostics.Select(CountOf));

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
        _offers = new OfferPresenter(TextMeasure, _listPalette);
        _tip = new MarkerTipPresenter(TextMeasure, _tipPalette);
        Clipboard = new AvaloniaClipboard(() => TopLevel.GetTopLevel(this)?.Clipboard);

        Background = Brushes.White;
        HighlightBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x26, 0x8B, 0xD2));
        CaretBrush = Brushes.Black;

        Focusable = true;
        ClipToBounds = true;

        // The whole surface is text the user can type into, so the pointer says so everywhere on it -
        // the arrow would read as "this is a panel you look at" rather than "this is where you write".
        // Which of the three shapes is in force is decided per pointer move; this is the resting one.
        Cursor = new Cursor(_pointer);
    }

    /// <summary>
    /// The measure that turns text into distances for this control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A session must be opened with this measure, or the layout will be in units that are not pixels
    /// and the text will be drawn at the wrong width. <see cref="OpenSession"/> does that; a session
    /// built elsewhere has to be given it explicitly.
    /// </para>
    /// <para>
    /// Replaced, never mutated, when the font size changes: <see cref="AvaloniaTextMeasure"/> caches
    /// shapes and distances keyed on the font it was built with, and the rest of the control treats the
    /// identity of a measure as "which layout is this" - so a new size means a new measure.
    /// </para>
    /// </remarks>
    public AvaloniaTextMeasure TextMeasure { get; private set; }

    /// <summary>The em size the document is drawn at, in device pixels.</summary>
    /// <remarks>
    /// <para>
    /// Setting it rebuilds the measure, re-lays-out the open document and re-measures the control, which
    /// is the whole feature: a bigger font is a projection that gives every character more room, so the
    /// caret, the hit test, the gutter width and the completion list all follow without knowing that a
    /// size exists.
    /// </para>
    /// <para>
    /// Only positivity is checked here. What range of sizes is comfortable, and how far one step goes, is
    /// the host's opinion - this control renders at whatever it is given, exactly as it accepts any
    /// <see cref="Typeface"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public double FontSize
    {
        get => TextMeasure.FontSize;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            if (value == TextMeasure.FontSize)
            {
                return;
            }

            TextMeasure = new AvaloniaTextMeasure(TextMeasure.Typeface, value, TextMeasure.Foreground);

            // The list is drawn on this surface and must keep the editor's line height, or its rows stop
            // matching the text they are offering to fill.
            _completion.TextMeasure = TextMeasure;
            _offers.TextMeasure = TextMeasure;
            _tip.TextMeasure = TextMeasure;

            // A box wrapped for the old font is sized for glyphs that are no longer on screen.
            ForgetTip();

            if (Session is { } session)
            {
                session.Measure = TextMeasure;
            }

            InvalidateMeasure();
            ScrollToCaret();
            InvalidateVisual();

            // A new font size is a new step, a new extent and - through the gutter, if it is showing
            // line numbers - a new viewport.
            RaiseScrollInvalidated();
        }
    }

    /// <summary>Raised when the caret or the selection moved.</summary>
    /// <remarks>
    /// A host paints a status line from this: the caret's line and column are the shell's to show and the
    /// control's to derive. It is raised after every edit and every move lands - <see cref="Show"/> is that
    /// funnel - and only when the selection really changed, because a repaint that left the caret where it
    /// was is not a reason to rebuild a status line.
    /// </remarks>
    public event EventHandler? SelectionChanged;

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

            // The new session's caret has not been announced, whatever the old one's was.
            _published = null;

            // A preview is a copy of one document, so it goes with the session that document belonged to.
            DropPreview();

            // A hit points into the layout of the session it was taken from, so a new document must not
            // inherit the old one's tip.
            ClearHover();

            InvalidateMeasure();
            ScrollToCaret();
            InvalidateVisual();

            // A different document is a different extent, and an offset that just went back to zero.
            RaiseScrollInvalidated();
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

    /// <summary>The colours a preview's ghost is drawn in.</summary>
    /// <remarks>
    /// Separate from <see cref="Palette"/> because the two answer different questions: the palette says what text
    /// means, this one says what an edit did to it. A host that themes the page sets both.
    /// </remarks>
    public PreviewPalette PreviewPalette
    {
        get => _previewPalette;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _previewPalette = value;
            _ghostPainter = null;
            InvalidateVisual();
        }
    }

    /// <summary>The preview to draw instead of the page, or <see langword="null"/> for the page itself.</summary>
    /// <remarks>
    /// <para>
    /// A ghost is drawn <em>instead of</em> the document, not over it: an overlay would keep the old positions and
    /// lie about the layout. While one is set, the control paints that frame - tints, dimmed text and the badge -
    /// and no caret, selection, gutter numbering or bulb, because none of those describe the copy.
    /// </para>
    /// <para>
    /// The host decides when this is set, which is what keeps the hover timing out of the control: a preview is
    /// built from a <c>PreviewSandbox</c> the host already owns, so the control never learns what an intention is.
    /// </para>
    /// </remarks>
    public GhostFrame? Ghost
    {
        get => _ghost;
        set
        {
            if (ReferenceEquals(_ghost, value))
            {
                return;
            }

            _ghost = value;
            _ghostPainter = null;
            InvalidateMeasure();
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
            _offers.Palette = value;

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

            // The gutter is part of the viewport arithmetic, so toggling it changes the text area without
            // a resize and without an edit.
            RaiseScrollInvalidated();
        }
    }

    /// <summary>Whether the margin shows a bulb where the caret has something to offer.</summary>
    /// <remarks>
    /// <para>
    /// On by default, because it is the only affordance the feature has that can be <em>found</em>: the
    /// gesture has to be known and the menu has to be opened. A host that paints its own margin turns it
    /// off and uses <see cref="QuickFixBulb"/> instead - the geometry is in the editing layer precisely so
    /// that a second renderer needs nothing from this control.
    /// </para>
    /// <para>
    /// Like the row numbers, it is part of the viewport arithmetic: its margin is reserved whether or not
    /// a line offers anything, so turning it on or off re-measures instead of merely repainting.
    /// </para>
    /// </remarks>
    public bool ShowBulb
    {
        get => _showBulb;

        set
        {
            if (_showBulb == value)
            {
                return;
            }

            _showBulb = value;

            InvalidateMeasure();
            InvalidateVisual();
            RaiseScrollInvalidated();
        }
    }

    /// <summary>Whether the gutter shows the fold arrows.</summary>
    /// <remarks>
    /// On by default, and safe because the column is zero-width unless a language nominates a collapse slot.
    /// A host that turns it off keeps folding reachable through the context menu, which is what an embedded
    /// editor in a table cell needs.
    /// </remarks>
    public bool ShowFoldTwisties
    {
        get => _showFoldTwisties;

        set
        {
            if (_showFoldTwisties == value)
            {
                return;
            }

            _showFoldTwisties = value;

            InvalidateMeasure();
            InvalidateVisual();
            RaiseScrollInvalidated();
        }
    }

    /// <summary>What a folded node's arrow looks like.</summary>
    /// <remarks>
    /// Text, not geometry: a chevron drawn as a <c>StreamGeometry</c> would be the first non-rectangle in
    /// this control's chrome, and a <c>&#x25B8;</c> is not in every monospace face. Two ASCII characters are
    /// the safe pair, and a host that wants chevrons sets these two properties.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is empty.</exception>
    public string CollapsedGlyph
    {
        get => _collapsedGlyph;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            if (_collapsedGlyph == value)
            {
                return;
            }

            _collapsedGlyph = value;
            InvalidateVisual();
        }
    }

    /// <summary>What an open node's arrow looks like.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is empty.</exception>
    public string ExpandedGlyph
    {
        get => _expandedGlyph;
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            if (_expandedGlyph == value)
            {
                return;
            }

            _expandedGlyph = value;
            InvalidateVisual();
        }
    }

    /// <summary>Which nodes are folded, forwarded to the session like <see cref="Markers"/>.</summary>
    /// <remarks>
    /// Assigning re-projects through the session, so the tree, the layout and the caret all follow. Reading
    /// it before a session exists answers "nothing folded" rather than throwing, which is what a host that
    /// initializes its bindings in any order needs.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public CollapsedNodeSet Collapsed
    {
        get => Session?.Collapsed ?? CollapsedNodeSet.Empty;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Session is { } session)
            {
                session.Collapsed = value;

                // The session re-projects; the host invalidates, like it does after any edit.
                InvalidateMeasure();
                RaiseScrollInvalidated();
                Show();
            }
        }
    }

    /// <summary>The fold arrows of the current layout, computed at most once per layout and fold set.</summary>
    private IReadOnlyList<FoldMark> Marks
    {
        get
        {
            if (Session is not { } session)
            {
                return [];
            }

            if (_foldMarks is null
                || !ReferenceEquals(_foldMarksOf, session.Layout)
                || !ReferenceEquals(_foldMarksSet, session.Collapsed))
            {
                _foldMarks = FoldMarks.Of(session, session.Layout, TextMeasure);
                _foldMarksOf = session.Layout;
                _foldMarksSet = session.Collapsed;
            }

            return _foldMarks;
        }
    }

    /// <summary>The colour the bulb is painted in.</summary>
    /// <remarks>
    /// One brush rather than a palette: the bulb means one thing - "there is something here" - so it has
    /// one colour, and a host that recolours its margin sets this.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IBrush BulbBrush
    {
        get => _bulbBrush;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_bulbBrush, value))
            {
                return;
            }

            _bulbBrush = value;
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

    /// <summary>What a check found wrong with the document, or the empty set when nothing did.</summary>
    /// <remarks>
    /// <para>
    /// Forwards to <see cref="EditorSession.Markers"/>, which owns the value; the control only repaints.
    /// Assigning here invalidates the visual, so a host that goes through this property never has to ask
    /// for a repaint itself. Assigning <c>Session.Markers</c> directly still works and still costs no
    /// rebuild - it just needs the host to call <see cref="Visual.InvalidateVisual"/> afterwards.
    /// </para>
    /// <para>
    /// Nothing is resolved here: a marker is decoration, so the tree, the layout and the caret are
    /// untouched, and the wave is computed from the layout that is already on screen.
    /// </para>
    /// <para>
    /// A new set also drops the tip. The tip is anchored on a <c>MarkerLine</c> taken from the set that
    /// was current when the pointer stopped, so keeping it would leave a box on screen quoting a finding
    /// the host has just withdrawn - and a host republishes after every edit, so the sentence the user is
    /// reading can go out of date while the pointer stands still.
    /// </para>
    /// </remarks>
    public MarkerSet Markers
    {
        get => Session?.Markers ?? MarkerSet.Empty;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Session is not { } session || ReferenceEquals(session.Markers, value))
            {
                return;
            }

            session.Markers = value;
            ClearHover();

            // A republished set means the document changed under whatever the pointer was describing, so a preview
            // that is running describes a state that no longer exists.
            InvalidatePreview();
            InvalidateVisual();
        }
    }

    /// <summary>What the checks offer to repair, or the empty set when they offer nothing.</summary>
    /// <remarks>
    /// <para>
    /// The companion of <see cref="Markers"/> and published the same way, because they are published
    /// together: a finding and its repair describe one state of the document, so a host that republished
    /// the markers and forgot the fixes would offer cures for problems the user has already dealt with.
    /// </para>
    /// <para>
    /// A fix set is not decoration - something will eventually read it to offer the repair - so this is
    /// the one set whose assignment may be read after the fact. Nothing is resolved here either: the
    /// repairs are offered against the model when one is picked, not when the set arrives.
    /// </para>
    /// </remarks>
    public FixSet Fixes
    {
        get => Session?.Fixes ?? FixSet.Empty;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Session is not { } session)
            {
                return;
            }

            session.Fixes = value;
        }
    }

    /// <summary>The colour each severity is stroked in.</summary>
    /// <remarks>
    /// Named for what it colours rather than for its type, following <see cref="Palette"/> and
    /// <see cref="ListPalette"/>: a member called <c>MarkerPalette</c> would shadow the type of that name
    /// inside this class. Recolouring is a repaint - no severity reaches the model, so no rectangle moves.
    /// </remarks>
    public MarkerPalette MarkerColors
    {
        get => _markerColors;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_markerColors, value))
            {
                return;
            }

            _markerColors = value;
            InvalidateVisual();
        }
    }

    /// <summary>What the box that describes a marker under the pointer looks like.</summary>
    /// <remarks>
    /// Separate from <see cref="MarkerColors"/> because the two are asked different questions: a wave says
    /// "something is wrong here" in one colour per severity, while the tip is one panel of text whatever
    /// severity it is describing. Recolouring is a repaint - no colour reaches the model.
    /// </remarks>
    public MarkerTipPalette TipPalette
    {
        get => _tipPalette;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_tipPalette, value))
            {
                return;
            }

            _tipPalette = value;
            _tip.Palette = value;
            InvalidateVisual();
        }
    }

    /// <summary>How much room the row numbers and the bulb take on the left edge, in device pixels.</summary>
    /// <remarks>
    /// <para>
    /// The width is the widest number the document needs plus the blank that separates it from the text,
    /// so it grows by one digit when the document reaches a row count with one more digit in it. Text is
    /// laid out to the right of the gutter and the gutter itself does not scroll with the document, the
    /// way a code editor's does not.
    /// </para>
    /// <para>
    /// The bulb's margin is reserved whenever <see cref="ShowBulb"/> is on, <em>not</em> when a line
    /// happens to offer a repair: a margin that appears would shift every cell of the document sideways,
    /// and a page that jumps because the caret moved onto a marked line is worse than a narrow blank that
    /// is sometimes empty.
    /// </para>
    /// </remarks>
    public double GutterWidth
    {
        get
        {
            var fold = FoldColumnWidth;

            var margin = fold
                + (_showLineNumbers
                    ? (LineNumbers.DigitsOf(Rows) * TextMeasure.Measure("0", TextEmphasis.None)) + (2 * GutterGap)
                    : 0)
                + (ShowBulb ? BulbMargin : 0)
                + (fold > 0 && !_showLineNumbers ? 2 * GutterGap : 0);

            // Only when there is a margin to separate from: with neither numbers, folds nor a bulb the page
            // starts at the control's own edge, and a blank there would be a margin with nothing on its left.
            return margin > 0 ? margin + PageMargin : 0;
        }
    }

    /// <summary>How wide the fold column is: zero unless a twisty is shown and something can be folded.</summary>
    /// <remarks>
    /// Zero rather than "reserved always", unlike the bulb's margin: the column exists only for a language
    /// that opts into folding, so a language that does not must keep the gutter it had. The mark list is
    /// cached, so asking this does not walk the document on every repaint.
    /// </remarks>
    private double FoldColumnWidth =>
        ShowFoldTwisties && Marks.Count > 0 ? TextMeasure.Measure("0", TextEmphasis.None) : 0;

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
    /// that already has a clipboard service, assigns its own <see cref="IEditorClipboard"/>.
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
    public IEditorClipboard Clipboard { get; set; }

    /// <summary>The horizontal scroll offset, in layout units.</summary>
    public double OffsetX => _offsetX;

    /// <summary>The vertical scroll offset, in layout units.</summary>
    public double OffsetY => _offsetY;

    /// <summary>The numbers a scroll host reads back, or nothing when no document is open.</summary>
    /// <remarks>
    /// Computed rather than cached. The offset changes far more often than the layout does, so a cached
    /// range would have to be invalidated at every place that says the numbers moved - which is exactly
    /// where a stale number would hide. The arithmetic is one clamp and one subtraction per axis.
    /// </remarks>
    private ScrollRange? Range => Session is { } session
        ? ScrollRange.Of(session.Layout, ViewportSize, _offsetX, _offsetY, session.Measure)
        : null;

    /// <summary>
    /// The document as the layout produced it - not raised to the viewport, so a document shorter than the
    /// window reports a smaller extent and a bar with <c>Auto</c> visibility stays hidden.
    /// </summary>
    Size IScrollable.Extent => SizeOf(Range?.Extent);

    /// <summary>
    /// The room the document is drawn in, in the same units as <see cref="IScrollable.Extent"/>.
    /// </summary>
    /// <remarks>
    /// This is the text area, so the gutter is not part of it. Reporting <see cref="Bounds"/> instead would
    /// let a bar scroll one gutter-width further than the document goes, because the maximum a bar offers
    /// is the extent minus this number.
    /// </remarks>
    Size IScrollable.Viewport => SizeOf(Range?.Viewport);

    /// <summary>The scroll offset, clamped to the range the document has.</summary>
    Vector IScrollable.Offset
    {
        get => VectorOf(Range?.Offset);

        // The setter is how a host moves the content - a bar drag, a kinetic gesture, a scroll into view
        // raised by an ancestor. It clamps like every other scroll path, so a host cannot put the view
        // past the end of the document.
        set => ScrollTo(value.X, value.Y);
    }

    bool IScrollable.CanHorizontallyScroll => _canScrollHorizontally;

    bool IScrollable.CanVerticallyScroll => _canScrollVertically;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canScrollHorizontally;
        set => _canScrollHorizontally = value;
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canScrollVertically;
        set => _canScrollVertically = value;
    }

    /// <summary>
    /// Always true: the control is laid out at the viewport and paints only the cells inside it, so its
    /// extent is a claim rather than a size the parent could arrange it at.
    /// </summary>
    /// <remarks>
    /// This is the flag that decides whether a <see cref="ScrollContentPresenter"/> measures the control
    /// with the available size or with an infinite one. Returning false would make the control ask for its
    /// whole document, which is a very wide control and a viewport that equals the extent.
    /// </remarks>
    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    /// <summary>One wheel notch: one indent of width, one line of height.</summary>
    Size ILogicalScrollable.ScrollSize => SizeOf(Range?.Step);

    /// <summary>One page: the viewport less a step, so a page never loses the line the reader was on.</summary>
    Size ILogicalScrollable.PageScrollSize => SizeOf(Range?.Page);

    /// <summary>Raised when the numbers above have changed; the host reads them back from here.</summary>
    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    /// <summary>Brings a rectangle of this control into view.</summary>
    /// <returns>Whether the offset moved.</returns>
    /// <remarks>
    /// The control paints its document instead of hosting a control per cell, so the only target that can
    /// arrive here meaningfully is the control itself, with a rectangle in its own coordinates - which is
    /// what <see cref="Control.BringIntoView()"/> raises. A descendant is refused rather than guessed at:
    /// it has no bounds of its own to honour, and scrolling to its parent's origin would be a lie.
    /// </remarks>
    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (Session is not { } session || !ReferenceEquals(target, this))
        {
            return false;
        }

        // A control coordinate is a layout coordinate moved past the gutter and up by the scroll.
        var rect = new LayoutRect(
            targetRect.X - GutterWidth + _offsetX,
            targetRect.Y + _offsetY,
            targetRect.Width,
            targetRect.Height);

        var (x, y) = LayoutViewport.ScrollIntoView(
            session.Layout,
            ViewportSize,
            _offsetX,
            _offsetY,
            rect);

        var (clampedX, clampedY) = LayoutViewport.Clamp(session.Layout, ViewportSize, x, y);
        var moved = clampedX != _offsetX || clampedY != _offsetY;

        ScrollTo(x, y);

        return moved;
    }

    /// <summary>Nothing to navigate to: the document's cells are painted, not hosted as controls.</summary>
    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;

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

        // The rebuild may have changed the document's size without moving the offset, and a scroll host
        // learns about a new extent only from this event.
        RaiseScrollInvalidated();
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
    /// <returns>Whether the offset moved, which is what tells a caller its gesture did something.</returns>
    /// <remarks>
    /// The answer is the difference between a wheel event this editor consumed and one it should hand
    /// back: a wheel over a document that already shows its first line belongs to whatever is behind the
    /// editor, and reporting "nothing happened" is the only way a host can forward it.
    /// </remarks>
    public bool ScrollTo(double x, double y)
    {
        var session = Session;

        if (session is null)
        {
            return false;
        }

        var (clampedX, clampedY) = LayoutViewport.Clamp(session.Layout, ViewportSize, x, y);

        if (clampedX == _offsetX && clampedY == _offsetY)
        {
            return false;
        }

        _offsetX = clampedX;
        _offsetY = clampedY;

        InvalidateVisual();

        // Guarded by the early return above: a host that set the offset we were already at must not be told
        // the offset moved, or its reaction to that would drive this setter again.
        RaiseScrollInvalidated();

        return true;
    }

    /// <summary>Tells a scroll host that what it reads back has changed.</summary>
    /// <remarks>
    /// The event is the whole notification channel: a logical scroll host never arranges this control at
    /// its extent and never measures it, so nothing else would reveal a new extent, viewport or offset.
    /// </remarks>
    private void RaiseScrollInvalidated(EventArgs? e = null)
    {
        _reported = (Range?.Extent ?? ViewportSize, ViewportSize);

        _scrollInvalidated?.Invoke(this, e ?? EventArgs.Empty);
    }

    /// <summary>Raises only if the extent or the viewport differs from what was last reported.</summary>
    /// <remarks>
    /// For the two sites that cannot know whether a resize changed anything: the bounds hook runs before
    /// the document is re-laid-out for the new width, and the end of the measure pass runs after. One of
    /// the two usually finds the numbers unchanged, and telling a host twice would make it re-read its
    /// bars for nothing on every layout pass.
    /// </remarks>
    private void RaiseScrollInvalidatedIfResized()
    {
        if ((Range?.Extent ?? ViewportSize, ViewportSize) != _reported && _scrollInvalidated is not null)
        {
            RaiseScrollInvalidated();
        }
    }

    /// <summary>Raises <see cref="ILogicalScrollable.ScrollInvalidated"/> on behalf of a caller.</summary>
    /// <remarks>
    /// The interface offers this so a wrapper can pass a notification through. There is no wrapper here,
    /// so it is the same raise the control does itself - and it stays guarded, so a host that calls it in
    /// response to the event cannot make the two of us spin.
    /// </remarks>
    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) => RaiseScrollInvalidated(e);

    /// <summary>The extent, viewport, offset, step or page as a <see cref="Size"/>, or nothing without a document.</summary>
    private static Size SizeOf(LayoutSize? size) => size is { } value ? new Size(value.Width, value.Height) : default;

    /// <summary>The offset as a <see cref="Vector"/>.</summary>
    private static Vector VectorOf(LayoutSize? offset) => offset is { } value ? new Vector(value.Width, value.Height) : default;

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

        // A preview replaces the page rather than covering it, so everything the page draws - gutter numbers,
        // bulb, selection, caret, lists, tips - belongs to a document that is not on screen. Only the ghost.
        if (_ghost is { } ghost)
        {
            GhostPainter().Render(
                context,
                ghost,
                new Rect(Bounds.Size),
                GutterWidth,
                Background ?? Brushes.White,
                _palette);

            return;
        }

        PaintGutter(context);

        // In the margin rather than in the page, so it is painted with the gutter and does not scroll
        // sideways with the text: the bulb names a line, not a column.
        PaintBulb(context, session);

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

        // After the text, so a squiggle is never buried by a glyph, and before the caret, so a caret that
        // stands on a marked character is still visible.
        PaintMarkers(context);

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

        // After the completion list, because the two are never open at once - an edit that opens one
        // closes the other - and before the tip, which is the only thing that may cover them.
        if (session.Offered is { } offered)
        {
            _offers.Render(context, offered, OfferBounds(session, offered));

            // The middle rung of the preview ladder: the hovered entry's own sentence. It belongs to the list, so it
            // sits under the box the pointer aimed at rather than under one row of it.
            PaintOfferTip(context, session);
        }

        // Before the finding's tip, so a squiggle's message is on top if the two ever overlap: it is the one
        // that asks for an action, and a description can be re-read by hovering again.
        PaintConceptTip(context);

        // After the list, because the two are read at different moments: the list is what the caret is
        // about to create, and a tip only exists while the pointer rests on a squiggle, which means the
        // reader is looking rather than typing. Whatever it covers is still one move away.
        PaintTip(context);
    }

    /// <summary>The painter for the ghost, rebuilt when the measure or the palette changed.</summary>
    private GhostPresenter GhostPainter() =>
        _ghostPainter ??= new GhostPresenter(TextMeasure, _previewPalette, _tip.Palette);

    /// <summary>Paints the message of the marker the pointer is on, if it is on one.</summary>
    /// <remarks>
    /// The box is placed from the <em>anchor's layout rectangle</em> rather than from the pointer, so it
    /// sits under the squiggle instead of under the cursor and cannot drift away from the text it
    /// describes while the pointer wanders inside the same wave.
    /// </remarks>
    private void PaintTip(DrawingContext context)
    {
        if (_hover is not { } hover || TipBounds(hover) is not { } bounds)
        {
            return;
        }

        _tip.Render(context, _tipLines!, bounds);
    }

    /// <summary>Where the tip's box goes, wrapping its text the first time it is asked for at a width.</summary>
    /// <param name="hover">The finding the pointer is on.</param>
    /// <returns>The box, or <see langword="null"/> when there is nothing to draw.</returns>
    private Rect? TipBounds(MarkerHit hover)
    {
        // The whole surface, gutter included: the anchor below is in control pixels, so the room the box
        // may use is the control's, not the page's.
        var viewport = new LayoutSize(Math.Max(0, Bounds.Width), Math.Max(0, Bounds.Height));

        // The cap keeps a long explanation from covering the document; the viewport term keeps a narrow
        // window from producing a box wider than the surface it is painted on.
        var room = Math.Min(
            MarkerTipPresenter.MaximumTextWidth,
            Math.Max(1, viewport.Width - (2 * MarkerTipPresenter.Inset)));

        // The counts are part of the key because they are part of the text: a host that republishes its
        // fixes while the pointer stands still would otherwise keep a tip that says "1 quick fix" over a
        // finding that now has three.
        var fixes = TopicFixes(hover.Diagnostics);

        if (_tipLines is null || _tipWidth != room || _tipFixes != fixes)
        {
            _tipLines = MarkerTip.Lines(hover.Diagnostics, room, TextMeasure, CountOf);
            _tipSize = _tip.Measure(_tipLines);
            _tipWidth = room;
            _tipFixes = fixes;
        }

        if (_tipLines.Count == 0)
        {
            return null;
        }

        // The same conversion PaintMarkers makes: the gutter is on the left of the page, so a layout unit
        // is a control pixel once the gutter is added back and the scroll is taken off.
        var anchor = new LayoutRect(
            hover.Anchor.X - _offsetX + GutterWidth,
            hover.Anchor.Y - _offsetY,
            hover.Anchor.Width,
            hover.Anchor.Height);

        var box = MarkerTip.Place(anchor, new LayoutSize(_tipSize.Width, _tipSize.Height), viewport);

        return new Rect(box.X, box.Y, box.Width, box.Height);
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

        // A pending hover delay outliving the control would show a tip for a document nobody is looking at.
        if (_conceptTipTimer is { } hover)
        {
            hover.Stop();
            _conceptTipTimer = null;
        }

        // The preview is a copy of the document plus a timer that ticks it; neither should outlive the surface.
        DropPreview();

        // A popup outlives the visual tree it was opened from, so a control that is detached (closed
        // tab, disposed window) has to take its menu with it.
        _menu?.Close();
        _menu = null;

        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The measure pass is where the editor learns how much room it has, so it publishes the width it is
    /// given as the page a text cell wraps at, and the height as the distance a page move travels. An
    /// unbounded width means there is no page: the document is laid out one long line at a time, which is
    /// what a host inside a scrolling panel asks for. An unbounded height means the same thing for the
    /// other axis - there is no page to move by, so the page keys fall back to the single-line move
    /// rather than jumping to the end of a document that is entirely on screen.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var session = Session;

        if (session is not null)
        {
            if (!double.IsInfinity(availableSize.Width))
            {
                // The gutter is the host's chrome, not part of the page, so the room a text cell may fill
                // is what is left of the width once the numbers have taken theirs.
                session.WrapWidth = Math.Max(0, availableSize.Width - GutterWidth);
            }

            // Only the width is chrome; the page runs the full height the host gave it.
            session.PageHeight = double.IsInfinity(availableSize.Height) ? 0 : Math.Max(0, availableSize.Height);
        }

        // Setting the wrap width re-lays-out the document, so the extent reported a moment ago may have
        // been the previous width's. A host needs the numbers that are on screen now.
        RaiseScrollInvalidatedIfResized();

        var content = session?.Layout.Size;

        // A control that fills its parent: the viewport is whatever the host gives it, and a document
        // shorter than the window still occupies the window so clicks below it land somewhere.
        return new Size(
            double.IsInfinity(availableSize.Width) ? content?.Width ?? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? content?.Height ?? 0 : availableSize.Height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A resize changes the viewport without touching the document, and a logical scroll host never
    /// arranges this control at its extent - so this notification is the only way the bars hear about a
    /// new window size. It fires before the measure pass, so it sees the old extent; the end of
    /// <see cref="MeasureOverride"/> checks again once the new width is in the layout.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);

        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            RaiseScrollInvalidatedIfResized();
        }
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
    /// <remarks>
    /// <para>
    /// The wheel is one axis at a time, as on most platforms: Shift turns the vertical wheel into the
    /// horizontal one, and a device that reports both moves on the axis it reports. One notch is
    /// <see cref="WheelLines"/> lines, because a document whose caret the mouse does not move should
    /// scroll in steps the user can aim.
    /// </para>
    /// <para>
    /// A wheel this control cannot act on is not consumed. A document already showing its first line has
    /// nowhere to go, and swallowing the event there would break the wheel for the panel the editor was
    /// dropped into - so the event is handed back untouched and the hover tip is kept, because nothing
    /// moved under the pointer.
    /// </para>
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerWheelChanged(e);

        if (Session is null)
        {
            return;
        }

        var horizontal = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var deltaX = horizontal ? e.Delta.Y : e.Delta.X;
        var deltaY = horizontal ? 0 : e.Delta.Y;
        var step = TextMeasure.LineHeight * WheelLines;

        if (!ScrollTo(_offsetX - (deltaX * step), _offsetY - (deltaY * step)))
        {
            return;
        }

        // The text moved under a pointer that did not, so the wave it was resting on is no longer there.
        // Dropping the tip is the honest answer; keeping it would describe text that scrolled away.
        ClearHover();

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

        // A press uses the pointer for something else - a selection, a menu - so the tip goes with it and
        // comes back on the next move, which is exactly when the reader is looking again.
        ClearHover();

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

        // The fold arrow is a target the user aimed at, so it reaches before the document: a press on it
        // must not also move the caret into the row it belongs to.
        if (FoldAt(e.GetPosition(this)) is { } mark)
        {
            session.ToggleCollapsed(mark.Node);

            InvalidateMeasure();
            RaiseScrollInvalidated();
            Show();

            e.Handled = true;

            return;
        }

        // The bulb is the first refusal, because it is a target the user aimed at: a press on it must not
        // also move the caret into the line it belongs to.
        if (ClickBulb(session, e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        // The list overlays the document, so it gets the first refusal: a click on a proposal must not
        // also move the caret into the slot the proposal belongs to.
        if (ClickCompletion(session, e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        // The offer list overlays the document in the same way, and for the same reason: a click on an
        // offer must not also move the caret into the line it belongs to.
        if (ClickOffer(session, e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        // A column boundary is a target the user aimed at, so it reaches before the document: a press on
        // it must not also move the caret into the header it belongs to.
        if (ColumnAt(session, e.GetPosition(this)) is { } handle)
        {
            _resizing = handle;
            _resizeOrigin = e.GetPosition(this).X;

            e.Pointer.Capture(this);
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

            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                // Ctrl+click is the pointer's spelling of Ctrl+B, and it deliberately starts no drag: the press is a
                // navigation, not the anchor of a selection.
                _ = session.Apply(new GoToDefinitionAction());
            }
            else if (e.ClickCount > 1)
            {
                // A second click means the word under the pointer, a third the whole value. Either way the
                // gesture deliberately does not start a drag: the first pointer move would collapse the
                // range onto the anchor the drag would set, and the selection the user asked for would
                // vanish before they could type over it.
                _ = session.Apply(e.ClickCount > 2 ? new SelectValueAction() : new SelectWordAction());
            }
            else
            {
                _dragAnchor = position;
                _dragging = true;

                e.Pointer.Capture(this);
            }
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

        if (session is null)
        {
            return;
        }

        if (_resizing is { } handle)
        {
            var point = e.GetPosition(this);
            var width = handle.Width + (point.X - _resizeOrigin);

            // A drag that would make the column vanish is a drag the reader did not mean; the floor is
            // one character, which is also the narrowest a column can be and still be pointed at.
            var floor = session.Measure.Measure(" ", TextEmphasis.None);

            session.SetColumnWidth(handle.Owner, handle.Slot, handle.Column, Math.Max(floor, width));

            InvalidateMeasure();
            RaiseScrollInvalidated();
            Show();

            return;
        }

        if (!_dragging || _dragAnchor is not { } anchor)
        {
            var point = e.GetPosition(this);

            UpdateOfferPreview(session, point);
            UpdateHover(point);
            UpdatePointer(session, point);

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
    /// <remarks>
    /// The pointer left the surface, so nothing under it is being pointed at any more. Without this the tip
    /// would stay on screen until the next move inside the control, describing a wave the pointer is no
    /// longer on.
    /// </remarks>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerExited(e);

        // The pointer is off the surface, so it is off the list too - and a preview of a row nobody is pointing at is
        // an answer to a question that has been withdrawn.
        LeavePreview();
        ClearHover();
    }

    /// <summary>Moves the tip onto the marker under the pointer, or off it when there is none.</summary>
    /// <param name="point">Where the pointer is, in control pixels.</param>
    private void UpdateHover(Point point)
    {
        // The gutter is chrome rather than page, so a pointer over the numbers is over no marker at all.
        // Everything right of it is converted the way PaintMarkers converts back: layout units in, minus
        // the scroll, plus the gutter.
        var gutter = GutterWidth;

        var hit = point.X < gutter
            ? null
            : MarkerHitTest.At(Lines, point.X - gutter + _offsetX, point.Y + _offsetY, MarkerWave.Band);

        if (IsSameHover(hit, _hover))
        {
            // A pointer moving inside one wave must not repaint: the tip already describes that finding.
            // The concept tip still has to follow the pointer, because leaving the wave re-enables it.
            UpdateConceptHover(point, hit);

            return;
        }

        _hover = hit;
        ForgetTip();
        UpdateConceptHover(point, hit);
        InvalidateVisual();
    }

    /// <summary>Picks the pointer shape for <paramref name="point"/>, which is a hand over anything a press acts on.</summary>
    /// <param name="session">The document under the pointer.</param>
    /// <param name="point">Where the pointer is, in control pixels.</param>
    /// <remarks>
    /// <para>
    /// The bulb and a fold arrow are the two things a press acts on, so they are the two shapes that promise
    /// something: a hand says "this is a target" where an I-beam would say "this is text". The bulb needs
    /// this most, because it sits in the margin beside the page - the same margin whose numbers and blank
    /// space do nothing - so without a distinct pointer the only thing telling the user the glyph is a
    /// button is the colour of a circle.
    /// </para>
    /// <para>
    /// The gutter itself stays an arrow: nothing there reacts to a press, and a hand would promise
    /// something the numbers do not do. Everything right of the gutter is the page and stays an I-beam,
    /// since that is where the user writes.
    /// </para>
    /// </remarks>
    private void UpdatePointer(EditorSession session, Point point)
    {
        var shape = PointerShapeAt(session, point);

        if (shape == _pointer)
        {
            return;
        }

        _pointer = shape;
        Cursor = new Cursor(shape);
    }

    /// <summary>Which pointer shape <paramref name="point"/> deserves.</summary>
    /// <param name="session">The document under the pointer.</param>
    /// <param name="point">Where the pointer is, in control pixels.</param>
    /// <remarks>
    /// The order is the one the press path uses: a target wins over the margin it sits in, and the page
    /// only answers once neither target is under the pointer.
    /// </remarks>
    private StandardCursorType PointerShapeAt(EditorSession session, Point point)
    {
        if (ColumnAt(session, point) is not null)
        {
            return StandardCursorType.SizeWestEast;
        }

        if (IsTarget(session, point))
        {
            return StandardCursorType.Hand;
        }

        return point.X < GutterWidth ? StandardCursorType.Arrow : StandardCursorType.Ibeam;
    }

    /// <summary>Whether a press at <paramref name="point"/> would do something.</summary>
    /// <param name="session">The document under the pointer.</param>
    /// <param name="point">Where the pointer is, in control pixels.</param>
    /// <remarks>
    /// <para>
    /// Both questions are asked of the same geometry the paint and the press use, so the shape under the
    /// pointer cannot disagree with what is drawn there or with what a click would reach.
    /// </para>
    /// <para>
    /// The gutter test comes first because both targets are in the margin and the pointer spends its life on
    /// the page: finding the bulb resolves the caret's cell and its text through the layout, which is work a
    /// move over ordinary text must not pay for.
    /// </para>
    /// </remarks>
    private bool IsTarget(EditorSession session, Point point) =>
        point.X < GutterWidth
        && ((BulbOnScreen(session) is { } bulb && bulb.Contains(point)) || FoldAt(point) is not null);

    /// <summary>The fold arrow under <paramref name="point"/>, if the point is in the twisty column.</summary>
    /// <param name="point">Where the pointer is, in control pixels.</param>
    private FoldMark? FoldAt(Point point)
    {
        if (!ShowFoldTwisties || point.X >= GutterWidth)
        {
            return null;
        }

        // The column starts at the gutter's own padding, and the marks are laid out from x = 0.
        return FoldHitTest.At(Marks, 0, FoldColumnWidth, point.X - GutterGap, point.Y + _offsetY);
    }

    /// <summary>Drops the tip, if one is up.</summary>
    private void ClearHover()
    {
        _hoverNode = null;
        _hoverSlot = null;
        CancelConceptTip();

        if (_hover is null)
        {
            return;
        }

        _hover = null;
        ForgetTip();
        InvalidateVisual();
    }

    /// <summary>Drops the wrapped tip but keeps the hover, so the next paint re-wraps where it is.</summary>
    private void ForgetTip()
    {
        _tipLines = null;
        _tipSize = default;
        _tipWidth = -1;
        _tipFixes = string.Empty;
    }

    /// <summary>Follows the pointer over ordinary text, which is where a concept tip may appear.</summary>
    /// <param name="point">Where the pointer is, in control pixels.</param>
    /// <param name="marker">The finding the pointer is on, if it is on one.</param>
    /// <remarks>
    /// <para>
    /// The delay exists because this tip's target is everything: a squiggle is a small shape a user aims at,
    /// while ordinary text is what the pointer crosses on its way somewhere else. Without the delay every
    /// mouse move would flash a box.
    /// </para>
    /// <para>
    /// The marker wins. A squiggle is a specific finding at a specific place and a description is ambient,
    /// so a pointer over a wave shows the finding and this tip stays down - never two boxes under one
    /// pointer.
    /// </para>
    /// </remarks>
    private void UpdateConceptHover(Point point, MarkerHit? marker)
    {
        if (marker is not null)
        {
            _hoverNode = null;
            _hoverSlot = null;
            CancelConceptTip();

            return;
        }

        var place = PlaceAt(point);
        var node = place?.Address.Owner;

        // A slot's own sentence exists while the slot is empty, and over a value the language asked for it too,
        // so asking the editing layer is also how the hover decides whether a slot or a node is what the pointer
        // is on.
        var slot = place is { } named && Session is { } session && PropertyTip.Of(session, named) is not null
            ? named.Address.Slot
            : null;

        if (node == _hoverNode && slot == _hoverSlot)
        {
            // The same target: whatever is up is still about the right thing, and a tip that has not appeared
            // yet must not restart its delay because the pointer moved inside the node.
            return;
        }

        _hoverNode = node;
        _hoverSlot = slot;
        CancelConceptTip();

        if (node is null)
        {
            return;
        }

        _conceptTipTimer ??= CreateConceptTipTimer();
        _conceptTipTimer.Stop();
        _conceptTipTimer.Start();
    }

    /// <summary>Shows the tip for the node the pointer has been resting on.</summary>
    private void ShowConceptTip()
    {
        _conceptTipTimer?.Stop();

        if (Session is not { } session || _hoverNode is not { } node)
        {
            return;
        }

        // Asked again rather than remembered: the pointer may have moved onto a wave while the timer ran. The
        // property's own sentence wins over the concept's, because it is the more specific answer - it says what
        // belongs in the box the pointer is over, not what kind of node the box is part of.
        if (_hover is not null)
        {
            return;
        }

        var slot = _hoverSlot is { } named ? CellAddress.OfSlot(node, named) : (CellAddress?)null;
        var content = (slot is { } address ? PropertyTip.Of(session, CaretPosition.At(address)) : null)
            ?? ConceptTip.Of(session, node);

        if (content is null)
        {
            return;
        }

        // A slot's tip is anchored to the slot's own cell: it is about that box, and a node drawn only as a
        // property has no cell carrying its node address at all. A concept's tip stays on the node, so it does
        // not jump while the pointer crosses one line of a node that spans several.
        _conceptTipAnchor = slot ?? new CellAddress(node);

        _conceptTip = content;
        _conceptTipNode = node;
        _conceptTipLines = null;
        InvalidateVisual();
    }

    /// <summary>Drops the concept tip that is showing, and the pending delay.</summary>
    /// <remarks>
    /// It deliberately leaves <see cref="_hoverNode"/> alone: the caller decides whether the pointer is now
    /// over nothing, over another node, or over the same node as before - and only the last of those must
    /// keep a delay that is already running.
    /// </remarks>
    private void CancelConceptTip()
    {
        _conceptTipTimer?.Stop();

        if (_conceptTip is null)
        {
            return;
        }

        _conceptTip = null;
        _conceptTipNode = null;
        _conceptTipAnchor = null;
        _conceptTipLines = null;
        _conceptTipSize = default;
        _conceptTipWidth = -1;
        InvalidateVisual();
    }

    /// <summary>The node under <paramref name="point"/>, or <see langword="null"/> over chrome or nothing.</summary>
    /// <remarks>
    /// The caret place the point resolves to names a cell, and a cell names the node it was projected from -
    /// which is the node the description belongs to. Over the margin there is no place at all, so no tip.
    /// </remarks>
    private NodeId? NodeAt(Point point) => PlaceAt(point)?.Address.Owner;

    /// <summary>The caret place under <paramref name="point"/>, or <see langword="null"/> over chrome.</summary>
    /// <remarks>
    /// The place rather than only its node, because a slot's own tooltip needs to know which cell the pointer is
    /// over - a node has as many properties as its concept declares.
    /// </remarks>
    private CaretPosition? PlaceAt(Point point)
    {
        if (Session is not { } session || point.X < GutterWidth)
        {
            return null;
        }

        var x = point.X - GutterWidth + _offsetX;
        var y = point.Y + _offsetY;

        return HitTest.Caret(session.Tree, session.Layout, x, y, session.Measure);
    }



    /// <summary>Paints the concept tip, unless a finding's tip is up instead.</summary>
    /// <remarks>
    /// Drawn before the marker tip, so the finding is on top if the two ever overlap: it is the one that asks
    /// for an action.
    /// </remarks>
    private void PaintConceptTip(DrawingContext context)
    {
        if (_conceptTip is not { } content || _hover is not null || ConceptTipBounds(content) is not { } bounds)
        {
            return;
        }

        _tip.Render(context, _conceptTipLines!, bounds);
    }

    /// <summary>Where the concept tip goes, wrapping its text the first time it is asked for at a width.</summary>
    /// <param name="content">What the tip says.</param>
    /// <returns>The box, or <see langword="null"/> when there is nothing to draw.</returns>
    private Rect? ConceptTipBounds(TipContent content)
    {
        var viewport = new LayoutSize(Math.Max(0, Bounds.Width), Math.Max(0, Bounds.Height));

        var room = Math.Min(
            MarkerTipPresenter.MaximumTextWidth,
            Math.Max(1, viewport.Width - (2 * MarkerTipPresenter.Inset)));

        if (_conceptTipLines is null || _conceptTipWidth != room)
        {
            _conceptTipLines = TipLines.Of(content, room, TextMeasure);
            _conceptTipSize = _tip.Measure(_conceptTipLines);
            _conceptTipWidth = room;
        }

        if (_conceptTipLines.Count == 0 || _conceptTipNode is not { } node || Session is not { } session)
        {
            return null;
        }

        var bounds = _conceptTipAnchor is { } wanted ? session.Layout.BoundsOf(wanted) : null;

        // Falling back to the node keeps a tip anchored even when the cell it is about has just gone.
        bounds ??= session.Layout.BoundsOf(new CellAddress(node));

        if (bounds is not { } anchor)
        {
            return null;
        }

        var placed = new LayoutRect(
            anchor.X - _offsetX + GutterWidth,
            anchor.Y - _offsetY,
            anchor.Width,
            anchor.Height);

        var box = MarkerTip.Place(placed, new LayoutSize(_conceptTipSize.Width, _conceptTipSize.Height), viewport);

        return new Rect(box.X, box.Y, box.Width, box.Height);
    }

    /// <summary>Builds the timer that waits before a concept tip appears.</summary>
    private DispatcherTimer CreateConceptTipTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ConceptTipDelayMs) };

        timer.Tick += (_, _) => ShowConceptTip();

        return timer;
    }

    /// <summary>Whether two hits describe the same finding.</summary>
    /// <remarks>
    /// <see cref="MarkerHit"/> cannot be compared with <c>==</c> here: it carries a list that
    /// <see cref="MarkerHitTest"/> builds afresh on every call, so two hits over one wave are never
    /// reference-equal. What identifies the finding is the rectangle it is drawn in and how many
    /// diagnostics landed on it - and a different set on the same stretch would have needed a different
    /// marker set, which invalidates the cache the hit was taken from anyway.
    /// </remarks>
    private static bool IsSameHover(MarkerHit? left, MarkerHit? right)
    {
        if (left is not { } a || right is not { } b)
        {
            return left is null && right is null;
        }

        return a.Anchor == b.Anchor && a.Diagnostics.Count == b.Diagnostics.Count;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerReleased(e);

        if (_resizing is not null)
        {
            _resizing = null;

            e.Pointer.Capture(null);
            return;
        }

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
        else if (action is SelectOfferAction)
        {
            // An arrow in an open list is the keyboard's version of hovering a row, so the entry the highlight
            // landed on is the entry the ladder starts climbing.
            AfterEdit(session.Apply(action));
            HighlightOffer(session);
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

            // A scroll host learns about a new extent only from this event.
            RaiseScrollInvalidated();
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
        RaiseScrollInvalidated();
        Show();
    }

    private void Show()
    {
        _blink.Reset();
        ScrollToCaret();

        // Every edit and every move lands here, and each one can move the wave the tip is describing. The
        // pointer has not moved, so nothing else would notice that its box now points at the wrong text.
        ClearHover();

        // The scroll offset this method just corrected is part of the question a ghost answers, so the preview is
        // brought into line after the page has settled rather than before.
        ShowPreview();

        // Every edit and every move lands here, which is why the host hears about the caret from here.
        if (Session is { } session)
        {
            PublishSelection(session);
        }

        InvalidateVisual();
    }

    /// <summary>Tells the host where the caret went, when it went anywhere.</summary>
    private void PublishSelection(EditorSession session)
    {
        if (_published == session.Selection)
        {
            return;
        }

        _published = session.Selection;

        SelectionChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>The table column boundary under <paramref name="point"/>, if any.</summary>
    /// <remarks>
    /// The tolerance is in device pixels and generous enough to hit without aiming - a boundary is a
    /// line, not a box - and the point is converted the same way <see cref="CaretAt"/> converts it, so a
    /// scrolled page grabs the boundary the reader sees rather than the one the layout remembers.
    /// </remarks>
    private ColumnHandle? ColumnAt(EditorSession session, Point point)
    {
        if (point.X < GutterWidth)
        {
            return null;
        }

        return ColumnResize.HandleAt(
            session,
            point.X - GutterWidth + _offsetX,
            point.Y + _offsetY,
            tolerance: 4);
    }

    /// <summary>Where the completion list is drawn, in viewport coordinates.</summary>
    /// <remarks>
    /// Below the caret and shifted right of it, flipped above when it would hang off the bottom edge,
    /// then clamped so it never leaves the surface. Keeping it inside the control rather than over the
    /// host's other content is the reason the list is painted here instead of in a popup.
    /// </remarks>
    private Rect CompletionBounds(EditorSession session, CompletionList list)
    {
        var size = _completion.Measure(list);

        return ListBounds(session, size, _completion.RowHeight);
    }

    /// <summary>Where the offer list is drawn, in viewport coordinates.</summary>
    /// <remarks>
    /// The same placement as the completion list, from the same helper: both are lists that belong to the
    /// caret's line and both must stay inside the surface. The list belongs to the marker's line rather
    /// than to a slot, but the caret is on that line by construction - the gesture that opens the list
    /// asks about the line the caret is drawn on.
    /// </remarks>
    private Rect OfferBounds(EditorSession session, OfferList list)
    {
        var size = _offers.Measure(list);

        return ListBounds(session, size, _offers.RowHeight);
    }

    /// <summary>Places a list of <paramref name="size"/> beside the caret, kept inside the surface.</summary>
    /// <remarks>
    /// Below the caret and shifted right of it, flipped above when it would hang off the bottom edge, then
    /// clamped so it never leaves the control. Keeping it inside the control rather than over the host's
    /// other content is the reason these lists are painted here instead of in a popup - and it is one
    /// helper rather than two, so the two lists cannot end up flipping at different heights.
    /// </remarks>
    private Rect ListBounds(EditorSession session, Size size, double rowHeight)
    {
        var caret = CaretBounds.Of(session.Tree, session.Layout, session.Selection.Caret, session.Measure);

        var left = (caret?.X ?? 0) - _offsetX + GutterWidth;
        var top = (caret is { } below ? below.Y - _offsetY + below.Height : 0) + rowHeight;

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

    /// <summary>Accepts the row of the offer list the pointer pressed, if it hit one.</summary>
    /// <returns>Whether the press was the list's rather than the document's.</returns>
    /// <remarks>
    /// The same two actions the keyboard runs - move the highlight, then accept - so a row cannot be taken
    /// through a path the keyboard would have refused, and a click on a dimmed row still goes through the
    /// edit layer's own refusal instead of around it.
    /// </remarks>
    private bool ClickOffer(EditorSession session, Point point)
    {
        if (session.Offered is not { } list
            || _offers.RowAt(list, OfferBounds(session, list), point) is not { } index)
        {
            return false;
        }

        var delta = index - list.Selected;

        if (delta != 0)
        {
            session.Apply(new SelectOfferAction(delta));
        }

        AfterEdit(session.Apply(new AcceptOfferAction()));

        return true;
    }

    /// <summary>Moves the preview onto the row under the pointer, and off it when the pointer is between rows.</summary>
    /// <param name="session">The document whose offer list is open.</param>
    /// <param name="point">Where the pointer is, in control pixels.</param>
    /// <remarks>
    /// <para>
    /// The first rung of the ladder is the list's own hover state, which is why a pointer crossing a row moves the
    /// highlight as well: a row that is previewed but not highlighted would describe one entry while looking as if
    /// it described another.
    /// </para>
    /// <para>
    /// A pointer beside the box is not a decision, so it gets the ladder's grace period rather than an immediate
    /// cancel - that is what lets the pointer travel to and from the bulb without the preview flickering.
    /// </para>
    /// </remarks>
    private void UpdateOfferPreview(EditorSession session, Point point)
    {
        if (session.Offered is not { } list)
        {
            return;
        }

        if (_offers.RowAt(list, OfferBounds(session, list), point) is not { } index)
        {
            var between = PreviewFor(session);

            between.LeaveList();
            ShowPreview();

            return;
        }

        var delta = index - list.Selected;

        if (delta != 0)
        {
            session.Apply(new SelectOfferAction(delta));
            InvalidateVisual();
        }

        HoverOffer(session, (session.Offered ?? list)[index]);
    }

    /// <summary>Starts the ladder on whatever the open list now highlights, for the arrow keys.</summary>
    /// <param name="session">The document whose offer list is open.</param>
    private void HighlightOffer(EditorSession session)
    {
        if (session.Offered is not { } list)
        {
            LeavePreview();

            return;
        }

        HoverOffer(session, list.SelectedEntry);
    }

    /// <summary>Points the ladder at <paramref name="entry"/> and makes sure its clock is running.</summary>
    private void HoverOffer(EditorSession session, Intention entry)
    {
        var preview = PreviewFor(session);

        preview.Hover(entry);
        StartPreviewTicker(preview);
        ShowPreview();
    }

    /// <summary>Ends any preview and puts the page back.</summary>
    private void LeavePreview()
    {
        _preview?.Leave();

        StopPreviewTicker();
        ShowPreview();
    }

    /// <summary>Brings the ghost on screen into line with what the ladder says.</summary>
    /// <remarks>
    /// Called from every path that can end a hover - an edit, a caret move, a closed list, a changed document - so
    /// the "a preview never outlives its list" rule is one statement rather than one per path.
    /// </remarks>
    private void ShowPreview()
    {
        if (Session is not { } session || _preview is not { } preview)
        {
            return;
        }

        if (session.Offered is null)
        {
            preview.Leave();
        }

        SetPreviewGhost(preview.Stage == PreviewStage.Preview
            ? preview.Ghost(ViewportSize, _offsetX, _offsetY, Styler)
            : null);

        if (!preview.IsRunning)
        {
            StopPreviewTicker();
        }
    }

    /// <summary>Puts <paramref name="frame"/> on screen, or takes back the one this control put there.</summary>
    /// <remarks>
    /// Only the ghost this control set is taken back: a host that assigned its own frame keeps it, because the two
    /// reasons to draw a ghost - the ladder and a host's own policy - must not overwrite each other's answer.
    /// </remarks>
    private void SetPreviewGhost(GhostFrame? frame)
    {
        if (ReferenceEquals(frame, _previewGhost))
        {
            return;
        }

        if (frame is not null || ReferenceEquals(Ghost, _previewGhost))
        {
            Ghost = frame;
        }

        _previewGhost = frame;
    }

    /// <summary>Silences a running preview because the document it describes has changed.</summary>
    /// <remarks>
    /// The entry stays hovered and stays quiet until the pointer leaves and returns, which is the ladder's own rule
    /// (<see cref="PreviewState.Invalidate"/>) rather than a second one written here.
    /// </remarks>
    private void InvalidatePreview()
    {
        _preview?.Invalidate();

        StopPreviewTicker();
        SetPreviewGhost(null);
    }

    /// <summary>Throws the preview away: its copy, its ladder and the ghost this control put on screen.</summary>
    private void DropPreview()
    {
        StopPreviewTicker();

        _previewTimer = null;

        _preview?.Dispose();
        _preview = null;

        SetPreviewGhost(null);
    }

    /// <summary>The preview of the offer list, built on first use against the session in force.</summary>
    private OfferPreview PreviewFor(EditorSession session) => _preview ??= new OfferPreview(session);

    /// <summary>Runs the preview clock while an entry is hovered, and stops it when nothing is.</summary>
    /// <remarks>
    /// The host owns the timer and the ladder never asks what time it is - the same arrangement as the caret blink,
    /// which is why the timing itself needs no window to be tested.
    /// </remarks>
    private void StartPreviewTicker(OfferPreview preview)
    {
        if (!preview.IsRunning)
        {
            StopPreviewTicker();

            return;
        }

        _previewTimer ??= CreatePreviewTicker();

        if (!_previewTimer.IsEnabled)
        {
            _previewTimer.Start();
        }
    }

    private DispatcherTimer CreatePreviewTicker()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PreviewTickMs) };

        timer.Tick += OnPreviewTick;

        return timer;
    }

    private void StopPreviewTicker() => _previewTimer?.Stop();

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        if (Session is not { } session || _preview is not { } preview)
        {
            StopPreviewTicker();

            return;
        }

        preview.Tick(TimeSpan.FromMilliseconds(PreviewTickMs));
        ShowPreview();
    }

    /// <summary>Paints the hovered offer's description under the list, unless the ladder has moved past it.</summary>
    /// <param name="context">The surface to paint on.</param>
    /// <param name="session">The document whose list is open.</param>
    /// <remarks>
    /// The description is not a nicety: a label says what an entry is called, and the sentence is what it would do.
    /// Hovering is the only way to read it because the list is a column of labels and there is no room in a row for
    /// prose - and the preview that follows is an answer about the result, never about the intent.
    /// </remarks>
    private void PaintOfferTip(DrawingContext context, EditorSession session)
    {
        if (_preview is not { Stage: PreviewStage.Tooltip, Entry: { } entry }
            || session.Offered is not { } list)
        {
            return;
        }

        var viewport = new LayoutSize(Math.Max(0, Bounds.Width), Math.Max(0, Bounds.Height));
        var room = Math.Min(
            MarkerTipPresenter.MaximumTextWidth,
            Math.Max(1, viewport.Width - (2 * MarkerTipPresenter.Inset)));

        if (_offerTipOf is not { } wrapped
            || !string.Equals(wrapped.Heading, entry.Label, StringComparison.Ordinal)
            || !string.Equals(wrapped.Description, entry.Description, StringComparison.Ordinal)
            || !wrapped.Room.Equals(room))
        {
            var content = new TipContent(entry.Label, entry.Description);

            _offerTipLines = TipLines.Of(content, room, TextMeasure);
            _offerTipSize = _tip.Measure(_offerTipLines);
            _offerTipOf = (entry.Label, entry.Description, room);
        }

        var box = OfferBounds(session, list);
        var anchor = new LayoutRect(box.X, box.Bottom, box.Width, 0);
        var placed = MarkerTip.Place(anchor, new LayoutSize(_offerTipSize.Width, _offerTipSize.Height), viewport);

        _tip.Render(context, _offerTipLines!, new Rect(placed.X, placed.Y, placed.Width, placed.Height));
    }

    /// <summary>The room the bulb reserves in the margin: one line square plus the gap to the page.</summary>
    private double BulbMargin => Math.Round(TextMeasure.LineHeight) + QuickFixBulb.Gap;

    /// <summary>Whether the caret has anything to offer, computed at most once per caret and drawing.</summary>
    /// <remarks>
    /// <para>
    /// The answer is kept because the caret blinks: a repaint per blink tick must not walk the document's
    /// findings or ask every registered source. The caret, the layout and both published sets identify the
    /// question - the layout is a fresh object after every rebuild, the caret is a value, and a republished
    /// set is a different object - so a stale answer cannot outlive the thing it describes.
    /// </para>
    /// <para>
    /// A source registered after the document opened is the one case this key does not describe; it becomes
    /// visible on the next caret move or the next change, which is why a host registers its intentions when
    /// it opens a document rather than in the middle of editing one.
    /// </para>
    /// </remarks>
    private bool BulbOffered(EditorSession session)
    {
        var key = (session.Layout, session.Selection.Caret, session.Markers, session.Fixes);

        if (_bulbOf is { } previous && previous.Equals(key))
        {
            return _bulbOffered;
        }

        _bulbOf = key;
        _bulbOffered = IntentionList.For(session).Count > 0;

        return _bulbOffered;
    }

    /// <summary>Where the bulb is on screen right now, or <see langword="null"/> when none is on it.</summary>
    /// <param name="session">The document whose caret the bulb belongs to.</param>
    /// <remarks>
    /// <para>
    /// One answer for three questions - what is painted, what a press reaches and what shape the pointer
    /// takes - so the three can never describe different rectangles. <see cref="BulbOffered"/> is part of
    /// it rather than the caller's business: the margin is reserved whether or not the caret has something
    /// to offer, so this is the only statement of "there is a bulb there".
    /// </para>
    /// <para>
    /// A bulb scrolled out of the viewport is neither painted nor clickable, which is why the viewport test
    /// is here and not in the painter.
    /// </para>
    /// </remarks>
    private Rect? BulbOnScreen(EditorSession session)
    {
        if (!ShowBulb
            || !BulbOffered(session)
            || QuickFixBulb.BoundsOf(session.Tree, session.Layout, session.Selection.Caret, session.Measure)
                is not { } bulb)
        {
            return null;
        }

        var bounds = new Rect(bulb.X + GutterWidth, bulb.Y - _offsetY, bulb.Width, bulb.Height);

        return bounds.Bottom <= 0 || bounds.Y >= Bounds.Height ? null : bounds;
    }

    /// <summary>Paints the bulb in the margin, level with the caret's line.</summary>
    /// <remarks>
    /// Two primitives rather than a glyph from a font: the bulb must not change the width the measure
    /// reports for the line it sits on, and a font glyph would depend on the host's font family. It is
    /// drawn from the same layout rectangle the hit test uses, so what is painted is what is clickable.
    /// </remarks>
    private void PaintBulb(DrawingContext context, EditorSession session)
    {
        if (BulbOnScreen(session) is not { } bounds)
        {
            return;
        }

        var radius = bounds.Width * 0.32;
        var centre = new Point(bounds.X + (bounds.Width / 2), bounds.Y + radius + (bounds.Height * 0.06));

        context.DrawEllipse(_bulbBrush, null, centre, radius, radius);

        // The stem, so the circle reads as a bulb rather than as a bullet that belongs to the line.
        context.FillRectangle(
            _bulbBrush,
            new Rect(centre.X - (radius * 0.45), centre.Y + radius, radius * 0.9, radius * 0.75));
    }

    /// <summary>Opens the offers at the caret when the pointer pressed the bulb.</summary>
    /// <returns>Whether the press was the bulb's rather than the document's.</returns>
    /// <remarks>
    /// The press runs the same action the gesture runs, so the bulb cannot open something the keyboard
    /// would not have opened. A press that misses the bulb is left to the document, which is why the
    /// rectangle is asked for rather than a radius around a point.
    /// </remarks>
    private bool ClickBulb(EditorSession session, Point point)
    {
        if (BulbOnScreen(session) is not { } bounds || !bounds.Contains(point))
        {
            return false;
        }

        AfterEdit(session.Apply(new OfferAction()));

        return true;
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

        // Left to right: the arrows, then the numbers. The arrow column is zero-width unless a language
        // opts into folding, so this loop does nothing for every language that does not.
        PaintFoldMarks(context);

        // The numbers are right-aligned against the bulb's margin rather than against the page, or they
        // would be painted underneath the bulb.
        var right = width - (ShowBulb ? BulbMargin : 0) - GutterGap;
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

    /// <summary>Paints the fold arrows, one per foldable node, in the leftmost gutter column.</summary>
    /// <param name="context">The surface to paint on.</param>
    /// <remarks>
    /// Each glyph is centred inside its own mark, so the arrow lines up with the text it belongs to rather
    /// than with the number of the row - the two columns stay independent.
    /// </remarks>
    private void PaintFoldMarks(DrawingContext context)
    {
        var column = FoldColumnWidth;

        if (column <= 0)
        {
            return;
        }

        var lineHeight = TextMeasure.LineHeight;

        foreach (var mark in Marks)
        {
            var top = mark.Bounds.Y - _offsetY;

            if (top + lineHeight <= 0 || top >= Bounds.Height)
            {
                continue;
            }

            var text = TextMeasure.Shaped(
                mark.IsCollapsed ? CollapsedGlyph : ExpandedGlyph,
                LineNumberBrush,
                TextEmphasis.None);

            context.DrawText(
                text,
                new Point(
                    GutterGap + Math.Max(0, (column - text.WidthIncludingTrailingWhitespace) / 2),
                    top));
        }
    }

    /// <summary>The findings as placed on the layout, computed at most once per layout and marker set.</summary>
    /// <remarks>
    /// Both the waves and the tip ask where the findings are, and geometry walks the whole document rather
    /// than the visible part of it, so neither may recompute it per repaint. The marker set is immutable and
    /// the layout is a fresh object after every rebuild, so identity on the pair answers "is this still the
    /// same drawing" - and the measure is in the key because a cell's position depends on it.
    /// </remarks>
    private IReadOnlyList<MarkerLine> Lines
    {
        get
        {
            var session = Session;

            if (session is null)
            {
                return [];
            }

            if (_lines is null
                || !ReferenceEquals(_linesOfLayout, session.Layout)
                || !ReferenceEquals(_linesOfMarkers, session.Markers)
                || !ReferenceEquals(_linesOfMeasure, session.Measure))
            {
                _lines = MarkerGeometry.Of(session.Layout, session.Markers, session.Measure);
                _linesOfLayout = session.Layout;
                _linesOfMarkers = session.Markers;
                _linesOfMeasure = session.Measure;
            }

            return _lines;
        }
    }

    /// <summary>The stretches of text to stroke, merged from <see cref="Lines"/>.</summary>
    /// <remarks>
    /// Derived state: the merge depends on the placed findings and nothing else, so the identity of that
    /// list is the whole invalidation. Merging is cheap next to placing, but it still allocates, and the
    /// caret blink repaints twice a second.
    /// </remarks>
    private IReadOnlyList<MarkerRun> Runs
    {
        get
        {
            var lines = Lines;

            if (_runs is null || !ReferenceEquals(_runsOfLines, lines))
            {
                _runs = MarkerRuns.Of(lines);
                _runsOfLines = lines;
            }

            return _runs;
        }
    }

    /// <summary>Paints a wave under every marked stretch that is on screen.</summary>
    /// <remarks>
    /// <para>
    /// Clipped to the text area, so a marker that is scrolled half out of the window is cut at the gutter
    /// rather than drawn over it - the numbers are the host's chrome and nothing of the page may cover
    /// them. The cull is per run and cheap, so a document with thousands of findings still costs only the
    /// waves in the window.
    /// </para>
    /// <para>
    /// One pass over the runs in reading order; a later run can never overlap an earlier one, because
    /// <see cref="MarkerRuns"/> already merged the overlapping ones.
    /// </para>
    /// </remarks>
    private void PaintMarkers(DrawingContext context)
    {
        var runs = Runs;

        if (runs.Count == 0)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        var gutter = GutterWidth;

        using var clip = context.PushClip(new Rect(gutter, 0, Math.Max(0, width - gutter), height));

        foreach (var run in runs)
        {
            var bounds = new Rect(run.X - _offsetX + gutter, run.Y - _offsetY, run.Width, run.Height);

            if (bounds.Bottom <= 0 || bounds.Y >= height || bounds.Right <= gutter || bounds.X >= width)
            {
                continue;
            }

            MarkerWave.Draw(context, _markerColors.BrushOf(run.Severity), run.Severity, bounds);
        }
    }
}
