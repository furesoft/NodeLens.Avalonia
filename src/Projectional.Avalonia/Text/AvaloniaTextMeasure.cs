using System.Globalization;

using Avalonia.Media;

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
/// keyed on the string alone because everything else about a shaped run is fixed by the constructor.
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

    private readonly Dictionary<string, FormattedText> _shaped = new(StringComparer.Ordinal);
    private readonly Typeface _typeface;
    private readonly double _fontSize;

    private IBrush _foreground;
    private double _lineHeight;
    private double _indentWidth;

    /// <summary>Creates a measure for one typeface at one size.</summary>
    /// <param name="typeface">The font to shape with.</param>
    /// <param name="fontSize">The em size in device pixels.</param>
    /// <param name="foreground">The brush text is painted in; black when omitted.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fontSize"/> is not positive.</exception>
    public AvaloniaTextMeasure(Typeface typeface, double fontSize, IBrush? foreground = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);

        _typeface = typeface;
        _fontSize = fontSize;
        _foreground = foreground ?? Brushes.Black;
    }

    /// <summary>The brush text is painted in.</summary>
    /// <remarks>
    /// Setting it drops the cache, because a shaped run carries the brush it was created with. Doing it
    /// this way keeps the common case - one brush for the life of the control - free of a per-draw brush
    /// assignment.
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
                _indentWidth = Measure("  ");
            }

            return _indentWidth;
        }
    }

    /// <inheritdoc />
    public double Measure(string text) => Shaped(text).WidthIncludingTrailingWhitespace;

    /// <summary>The shaped form of <paramref name="text"/>, for drawing.</summary>
    /// <remarks>
    /// Drawing goes through the same cache as measuring, so a cell that was laid out is never shaped
    /// again to be painted.
    /// </remarks>
    public FormattedText Shaped(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (_shaped.TryGetValue(text, out var cached))
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
            _typeface,
            _fontSize,
            _foreground)
        {
            // Cells never wrap; the layout decides where a line ends, and a run that wrapped would
            // paint outside the box its own layout gave it.
            MaxTextWidth = double.PositiveInfinity,
            Trimming = TextTrimming.None,
        };

        _shaped.Add(text, formatted);

        return formatted;
    }

    public override string ToString() => $"{_typeface.FontFamily} {_fontSize}";
}
