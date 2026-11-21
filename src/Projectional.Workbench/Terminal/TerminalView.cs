using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using VtNetCore.VirtualTerminal.Layout;

namespace Projectional.Workbench;

/// <summary>A terminal's screen as a control: a grid of character cells painted from the emulator's page.</summary>
/// <remarks>
/// <para>
/// The control owns no emulation. It is handed a <see cref="TerminalSession"/>, tells it how many cells fit whenever it
/// is resized, and paints what the session answers - so a picture of a terminal and a test of a terminal's text are
/// the same object in two situations, and neither can drift from the other.
/// </para>
/// <para>
/// <b>A span has no column.</b> The runs the emulator answers with tile a row from its left edge, so the painter walks
/// them and adds up the widths it has already drawn. Assuming a span starts where the previous one ended is only true
/// for a monospaced face - which is what a terminal is - so the column count of a span is its text length.
/// </para>
/// <para>
/// The caret is drawn only while the control has the focus: a terminal without focus has no cursor, and painting one
/// anyway would claim the keys go to it.
/// </para>
/// </remarks>
internal sealed class TerminalView : Control
{
    /// <summary>The font size a terminal is drawn at when a host does not choose one.</summary>
    internal const double DefaultFontSize = 13;

    /// <summary>How many shaped strings are kept, which is what keeps a keystroke from re-shaping a whole screen.</summary>
    private const int ShapeCacheLimit = 512;

    /// <summary>Defines the <see cref="FontSize"/> property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(FontSize), DefaultFontSize);

    /// <summary>Defines the <see cref="FontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<TerminalView, FontFamily>(nameof(FontFamily), FontFamily.Default);

    private readonly Dictionary<ShapedKey, FormattedText> _shaped = [];

    private TerminalSession? _session;
    private TerminalPalette _palette = TerminalPalette.Light;
    private (double Width, double Height) _cell;

    /// <summary>A terminal screen with nothing in it.</summary>
    /// <remarks>
    /// Public although the type is not: the markup instantiates a control through its constructor, and Avalonia's
    /// loader looks for a public one. The type's own accessibility is what keeps it inside this assembly.
    /// </remarks>
    public TerminalView()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Raised for every byte the user's input stands for, which is what the panel sends to the program.</summary>
    internal event EventHandler<TerminalOutputEventArgs>? Input;

    /// <summary>The screen this control paints, or <see langword="null"/> while there is nothing to show.</summary>
    internal TerminalSession? Session
    {
        get => _session;
        set
        {
            if (ReferenceEquals(_session, value))
            {
                return;
            }

            if (_session is not null)
            {
                _session.Changed -= OnSessionChanged;
            }

            _session = value;

            if (_session is not null)
            {
                _session.Changed += OnSessionChanged;
            }

            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The brushes the cells are painted with.</summary>
    internal TerminalPalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            _shaped.Clear();
            InvalidateVisual();
        }
    }

    /// <summary>The size the terminal is drawn at.</summary>
    internal double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The font the terminal is drawn in, which a host should make a monospaced one.</summary>
    internal FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>The size of one character cell, which is what the control is measured in.</summary>
    internal (double Width, double Height) CellSize => Cell;

    /// <summary>The size of one character cell, which is what the control is measured in.</summary>
    private (double Width, double Height) Cell
    {
        get
        {
            if (_cell is { Width: > 0, Height: > 0 })
            {
                return _cell;
            }

            // "M" rather than an average: in a monospaced face every advance is the same, and the widest common
            // character is what a cell has to hold or a line would overlap the one above it.
            var advance = Shape("M", _palette.Foreground, bold: false, italic: false);

            _cell = (Math.Max(1, advance.WidthIncludingTrailingWhitespace), Math.Max(1, advance.Height));

            return _cell;
        }
    }

    /// <summary>How many cells fit in the control's current size.</summary>
    private (int Columns, int Rows) CellsInSize(Size size)
    {
        var cell = Cell;

        return (
            Math.Max(1, (int)Math.Floor(size.Width / cell.Width)),
            Math.Max(1, (int)Math.Floor(size.Height / cell.Height)));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_session is not { } session)
        {
            return default;
        }

        var cell = Cell;

        // The cells the session has are the size it wants; a panel that gives more room gets more cells in
        // ArrangeOverride rather than a stretched grid, which is how a terminal behaves.
        return new Size(session.Columns * cell.Width, session.Rows * cell.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_session is { } session)
        {
            var (columns, rows) = CellsInSize(finalSize);

            session.Resize(columns, rows);
        }

        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FontSizeProperty || change.Property == FontFamilyProperty)
        {
            _cell = default;
            _shaped.Clear();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);

        context.FillRectangle(_palette.Background, bounds);

        if (_session is not { } session)
        {
            return;
        }

        var cell = Cell;
        var rows = session.Page();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            DrawRow(context, rows[rowIndex], rowIndex, cell);
        }

        DrawCursor(context, session, cell);
    }

    /// <summary>Paints one row of spans, left to right.</summary>
    private void DrawRow(DrawingContext context, LayoutRow row, int rowIndex, (double Width, double Height) cell)
    {
        var top = rowIndex * cell.Height;

        // A double-width row is the emulator telling us the program asked for a wide line; drawing it in one column's
        // width would overlap the next row's text, so the row is drawn at half its column count per cell.
        var columnWidth = row.DoubleWidth ? cell.Width * 2 : cell.Width;
        var left = 0.0;

        foreach (var span in row.Spans)
        {
            if (string.IsNullOrEmpty(span.Text))
            {
                continue;
            }

            var width = span.Text.Length * columnWidth;

            if (span.BackgroundColor is { Length: > 0 } background && background != "#000000")
            {
                context.FillRectangle(
                    _palette.BackgroundOf(background),
                    new Rect(left, top, width, cell.Height));
            }

            // A hidden cell is a password field: the program put the text there and asked not to have it shown.
            if (!span.Hidden)
            {
                var text = Shape(span.Text, _palette.ForegroundOf(span.ForgroundColor), span.Bold, span.Italic);

                context.DrawText(text, new Point(left, top));
            }

            if (span.Underline)
            {
                context.DrawLine(
                    new Pen(_palette.ForegroundOf(span.ForgroundColor)),
                    new Point(left, top + cell.Height - 1),
                    new Point(left + width, top + cell.Height - 1));
            }

            left += width;
        }
    }

    /// <summary>Paints the cursor as a block over the cell it stands on.</summary>
    private void DrawCursor(DrawingContext context, TerminalSession session, (double Width, double Height) cell)
    {
        if (!IsFocused || !session.ShowCursor)
        {
            return;
        }

        var (column, row) = session.Cursor;

        if (row < 0 || row >= session.Rows || column < 0 || column >= session.Columns)
        {
            return;
        }

        context.FillRectangle(
            _palette.Cursor,
            new Rect(column * cell.Width, row * cell.Height, cell.Width, cell.Height));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        Focus();

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (_session is not { } session)
        {
            return;
        }

        // One notch is three rows, which is what a terminal's own scrollback step is: a line per notch would take a
        // hundred notches to reach anything.
        session.TopRow += e.Delta.Y > 0 ? -3 : 3;

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || _session is not { } session)
        {
            return;
        }

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // A control chord is a control character, and only for a letter: Ctrl+Up is a named key with a modifier, and
        // the emulator's table answers for it below.
        if (control && TerminalKeyMap.ControlLetterOf(e.Key) is { } letter)
        {
            Send(session.Key(letter, shift: false, control: true));

            e.Handled = true;

            return;
        }

        if (TerminalKeyMap.NameOf(e.Key) is { } name)
        {
            Send(session.Key(name, shift, control));

            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (e.Handled || _session is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        Send(System.Text.Encoding.UTF8.GetBytes(e.Text));

        e.Handled = true;
    }

    /// <summary>Repaints after the session consumed something.</summary>
    /// <remarks>
    /// A repaint and nothing else: the tree of cells is the session's, so a new row can never change the size the
    /// control was measured at - only what is painted in it.
    /// </remarks>
    private void OnSessionChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>Raises <see cref="Input"/> for bytes that were produced by the user.</summary>
    private void Send(byte[] bytes)
    {
        if (bytes.Length > 0)
        {
            Input?.Invoke(this, new TerminalOutputEventArgs(bytes));
        }
    }

    /// <summary>The shaped form of <paramref name="text"/> in one attribute combination, cached per combination.</summary>
    private FormattedText Shape(string text, IBrush brush, bool bold, bool italic)
    {
        var key = new ShapedKey(text, brush, bold, italic);

        if (_shaped.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var typeface = new Typeface(
            FontFamily,
            italic ? FontStyle.Italic : FontStyle.Normal,
            bold ? FontWeight.Bold : FontWeight.Normal);

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            brush);

        // A screen is repainted on every keystroke, so the cache is what keeps a long scrollback from re-shaping every
        // visible run each time. It is cleared wholesale rather than by eviction: a screen holds a few hundred runs and
        // a cache that grew without a bound would be the leak this is here to avoid.
        if (_shaped.Count >= ShapeCacheLimit)
        {
            _shaped.Clear();
        }

        _shaped[key] = formatted;

        return formatted;
    }

    /// <summary>The identity of a shaped string: the text, the brush and the two faces it can be drawn in.</summary>
    private readonly record struct ShapedKey(string Text, IBrush Brush, bool Bold, bool Italic);
}
