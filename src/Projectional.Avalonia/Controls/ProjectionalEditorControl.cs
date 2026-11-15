using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

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

    private double _offsetX;
    private double _offsetY;

    // Where the press that started a drag landed. Held here rather than in the session because it is
    // the mouse's memory of the gesture, not the document's.
    private CaretPosition? _dragAnchor;

    private bool _dragging;

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
        TextMeasure = new AvaloniaTextMeasure(typeface, fontSize, Brushes.Black);

        Background = Brushes.White;
        HighlightBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x26, 0x8B, 0xD2));
        CaretBrush = Brushes.Black;

        Focusable = true;
        ClipToBounds = true;
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

            InvalidateMeasure();
            ScrollToCaret();
            InvalidateVisual();
        }
    }

    /// <summary>What is painted behind the text; nothing when <see langword="null"/>.</summary>
    public IBrush? Background { get; set; }

    /// <summary>The colour text is painted in.</summary>
    public IBrush Foreground
    {
        get => TextMeasure.Foreground;
        set
        {
            TextMeasure.Foreground = value;
            InvalidateVisual();
        }
    }

    /// <summary>The colour selected text is painted over.</summary>
    public IBrush HighlightBrush { get; set; }

    /// <summary>The colour of the caret bar.</summary>
    public IBrush CaretBrush { get; set; }

    /// <summary>The horizontal scroll offset, in layout units.</summary>
    public double OffsetX => _offsetX;

    /// <summary>The vertical scroll offset, in layout units.</summary>
    public double OffsetY => _offsetY;

    /// <summary>Opens <paramref name="document"/> for editing and makes it what the control shows.</summary>
    /// <param name="document">The root of the model to edit.</param>
    /// <param name="notations">The notation that projects the document's concepts.</param>
    /// <returns>The session, which the caller owns and must dispose.</returns>
    public EditorSession OpenSession(Node document, NotationRegistry notations)
    {
        var session = new EditorSession(document, notations, TextMeasure);
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
            session.Measure);

        if (Background is { } background)
        {
            context.FillRectangle(background, new Rect(Bounds.Size));
        }

        foreach (var highlight in frame.Highlights)
        {
            context.FillRectangle(HighlightBrush, ToRect(frame.Viewport.ToViewport(highlight)));
        }

        foreach (var run in frame.TextRuns)
        {
            var bounds = frame.Viewport.ToViewport(run.Bounds);

            context.DrawText(TextMeasure.Shaped(run.Text), new Point(bounds.X, bounds.Y));
        }

        if (frame.CaretRect is { } caret)
        {
            var bounds = frame.Viewport.ToViewport(caret);

            context.FillRectangle(CaretBrush, new Rect(bounds.X, bounds.Y, CaretThickness, bounds.Height));
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var content = Session?.Layout.Size;

        // A control that fills its parent: the viewport is whatever the host gives it, and a document
        // shorter than the window still occupies the window so clicks below it land somewhere.
        return new Size(
            double.IsInfinity(availableSize.Width) ? content?.Width ?? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? content?.Height ?? 0 : availableSize.Height);
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

        if (session is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();

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

        AfterEdit(session.Apply(KeyRouter.For(key)));
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

        var result = session.Apply(KeyRouter.Type(e.Text));

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

    private void AfterEdit(EditResult result)
    {
        if (result.Changed)
        {
            // The document grew or shrank, so the space it wants changed as well.
            InvalidateMeasure();
        }

        Show();
    }

    private void Show()
    {
        ScrollToCaret();
        InvalidateVisual();
    }

    private CaretPosition? CaretAt(PointerEventArgs e)
    {
        var session = Session;

        if (session is null)
        {
            return null;
        }

        var point = e.GetPosition(this);

        // Pointer coordinates are device pixels; the layout's are its own units, shifted by the scroll.
        return HitTest.Caret(
            session.Tree,
            session.Layout,
            point.X + _offsetX,
            point.Y + _offsetY,
            session.Measure);
    }

    private LayoutSize ViewportSize => new(Bounds.Width, Bounds.Height);

    private static Rect ToRect(LayoutRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
