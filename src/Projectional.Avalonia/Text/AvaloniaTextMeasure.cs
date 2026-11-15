using System.Globalization;

using Avalonia.Media;

using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Avalonia;

/// <summary>
/// Measures and shapes text with real glyphs, and keeps the shaped result so the same string is not
/// laid out twice.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place where the editor stops being unit-free. Everything upstream speaks in layout
/// units; here a unit becomes one device pixel, and <see cref="LineHeight"/> becomes the real line
/// height of the font. Swapping this measure for <see cref="MonospaceTextMeasure"/> therefore changes
/// every number in a layout without changing a single line of arrangement code, which is what makes the
/// arrangement testable without a window.
/// </para>
/// <para>
/// Shaping is expensive - roughly an order of magnitude more than a dictionary lookup - and a frame
/// asks for the width of every cell it draws plus a prefix width for the caret. The cache is what keeps
/// rendering proportional to the cells on screen rather than to the cost of shaping them, and it is
/// keyed on the three things about a shaped run that vary - the string, the brush and the emphasis -
/// because anything else is fixed by the constructor.
/// </para>
/// <para>
/// Widths are measured including trailing whitespace, because the layout deliberately renders spaces as
/// cells; measuring them as zero would make every cell after a space overlap its neighbour.
/// </para>
/// </remarks>
public sealed class AvaloniaTextMeasure : ITextMeasure
{
    /// <summary>
    /// How many shaped strings are kept before the cache is dropped wholesale.
    /// </summary>
    /// <remarks>
    /// A document's distinct cell texts are few, but a session rebuilt on every keystroke sees a new
    /// string for every property cell that changed. The cap keeps a long editing session from growing
    /// without bound; dropping everything rather than evicting one entry is deliberate, because a miss
    /// costs one shaping and the bookkeeping for a real eviction policy would cost more than the misses
    /// it saves at this size.
    /// </remarks>
    private const int Capacity = 8192;

    private static readonly CultureInfo Culture = CultureInfo.CurrentCulture;

    private readonly Dictionary<ShapedTextKey, FormattedText> _shaped = new();
    private readonly Typeface _typeface;
    private readonly double _fontSize;

    private IBrush _foreground;
    private double _lineHeight;
    private double _indentWidth;

    /// <summary>Creates a measure for one typeface at one size.</summary>
    /// <param name="typeface">The font to shape with.</param>
    /// <param name="fontSize">The em size in device pixels.</param>
    /// <param name="foreground">The brush text is shaped in when no other is given; black when omitted.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize"/> is not positive.</exception>
    public AvaloniaTextMeasure(Typeface typeface, double fontSize, IBrush? foreground = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);

        _typeface = typeface;
        _fontSize = fontSize;
        _foreground = foreground ?? Brushes.Black;
    }

    /// <summary>The brush text is shaped in when no other brush is given.</summary>
    /// <remarks>
    /// Setting it drops the cache. It no longer has to - the key holds the brush - but every entry
    /// shaped with the old brush is now unreachable, and dropping them is cheaper than keeping them
    /// until the cap evicts the lot.
    /// </remarks>
    public IBrush Foreground
    {
        get => _foreground;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_foreground, value))
            {
                return;
            }

            _foreground = value;
            _shaped.Clear();
        }
    }

    /// <summary>The font text is shaped with.</summary>
    public Typeface Typeface => _typeface;

    /// <summary>The em size text is shaped at.</summary>
    public double FontSize => _fontSize;

    /// <summary>
    /// The height of one line, taken from the font rather than from a constant, so a tall font does not
    /// overlap the line below it.
    /// </summary>
    public double LineHeight
    {
        get
        {
            if (_lineHeight <= 0)
            {
                // Ascender and descender together: "Mg" reaches both extremes, and a string of capitals
                // would under-report the height of everything with a tail.
                _lineHeight = Shaped("Mg").Height;
            }

            return _lineHeight;
        }
    }

    /// <summary>
    /// One indentation level: two spaces, which is the width
    /// <see cref="MonospaceTextMeasure"/> calls two columns, so an indented layout looks the same under
    /// either measure.
    /// </summary>
    public double IndentWidth
    {
        get
        {
            if (_indentWidth <= 0)
            {
                _indentWidth = Measure("  ", TextEmphasis.None);
            }

            return _indentWidth;
        }
    }

    /// <inheritdoc />
    public double Measure(string text, TextEmphasis emphasis) =>
        Shaped(text, _foreground, emphasis).WidthIncludingTrailingWhitespace;

    /// <summary>The shaped form of <paramref name="text"/>, for drawing.</summary>
    /// <remarks>
    /// Drawing goes through the same cache as measuring, so a cell that was laid out is never shaped
    /// again to be painted - as long as it is painted in the measure's own <see cref="Foreground"/>.
    /// A highlighted run is shaped once more in its own colour, which colour cannot change the width of,
    /// so the layout stays right either way.
    /// </remarks>
    public FormattedText Shaped(string text) => Shaped(text, _foreground, TextEmphasis.None);

    /// <summary>The shaped form of <paramref name="text"/> as it is to be painted.</summary>
    /// <param name="text">The text to shape.</param>
    /// <param name="brush">The brush to paint it with.</param>
    /// <param name="emphasis">The face and the lines to draw it with.</param>
    /// <returns>The shaped run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="brush"/> is <see langword="null"/>.</exception>
    public FormattedText Shaped(string text, IBrush brush, TextEmphasis emphasis)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(brush);

        var key = new ShapedTextKey(text, brush, emphasis);

        if (_shaped.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_shaped.Count >= Capacity)
        {
            _shaped.Clear();
        }

        var formatted = new FormattedText(
            text,
            Culture,
            FlowDirection.LeftToRight,
            TypefaceOf(emphasis),
            _fontSize,
            brush)
        {
            // Cells never wrap; the layout decides where a line ends, and a run that wrapped would
            // paint outside the box its own layout gave it.
            MaxTextWidth = double.PositiveInfinity,
            Trimming = TextTrimming.None,
        };

        if (DecorationsOf(emphasis) is { } decorations)
        {
            formatted.SetTextDecorations(decorations);
        }

        _shaped.Add(key, formatted);

        return formatted;
    }

    /// <summary>
    /// The face to shape with: the measure's own typeface, made heavier or slanted as asked.
    /// </summary>
    /// <remarks>
    /// Only the style and the weight vary, and both are taken from the family the host configured, so a
    /// bold keyword comes from the same font as the text around it rather than from a default.
    /// </remarks>
    private Typeface TypefaceOf(TextEmphasis emphasis)
    {
        var weight = emphasis.HasFlag(TextEmphasis.Bold) ? FontWeight.Bold : _typeface.Weight;
        var style = emphasis.HasFlag(TextEmphasis.Italic) ? FontStyle.Italic : _typeface.Style;

        return weight == _typeface.Weight && style == _typeface.Style
            ? _typeface
            : new Typeface(_typeface.FontFamily, style, weight, _typeface.Stretch);
    }

    /// <summary>The lines to draw under or through the text, or <see langword="null"/> for none.</summary>
    /// <remarks>
    /// A <see cref="TextDecorationCollection"/> cannot be empty and still mean something, so the case of
    /// no lines is answered with nothing at all and the run is left undecorated.
    /// </remarks>
    private static TextDecorationCollection? DecorationsOf(TextEmphasis emphasis)
    {
        var underline = emphasis.HasFlag(TextEmphasis.Underline);
        var strikethrough = emphasis.HasFlag(TextEmphasis.Strikethrough);

        if (!underline && !strikethrough)
        {
            return null;
        }

        var decorations = new TextDecorationCollection();

        if (underline)
        {
            decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        }

        if (strikethrough)
        {
            decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        }

        return decorations;
    }

    public override string ToString() => $"{_typeface.FontFamily} {_fontSize}";
}
