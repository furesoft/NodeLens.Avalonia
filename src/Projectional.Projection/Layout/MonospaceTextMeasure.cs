using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Measures text as columns of a fixed-width font, so that layout and the text grid agree exactly.
/// </summary>
/// <remarks>
/// <para>
/// One character is one unit and one line is one unit high, which makes a laid-out model renderable
/// as a character grid with no rounding anywhere. That is what notation tests compare against, and it
/// is why this measure - not a real font - is the default for snapshots.
/// </para>
/// <para>
/// It is deliberately not the measure the editor control will use. Real glyphs have real widths, and
/// the point of <see cref="ITextMeasure"/> is that swapping this for a font-backed measure changes no
/// arrangement code.
/// </para>
/// </remarks>
public sealed class MonospaceTextMeasure : ITextMeasure
{
    /// <summary>Indentation is two columns, which is wide enough to read as a level and narrow enough to nest.</summary>
    public const double Indent = 2;

    private MonospaceTextMeasure()
    {
    }

    /// <summary>The one instance: the measure holds no state, so callers need no identity.</summary>
    public static MonospaceTextMeasure Instance { get; } = new();

    /// <summary>
    /// One column per character, whatever the emphasis.
    /// </summary>
    /// <remarks>
    /// A monospaced face keeps its advance when it is made bold, so a bold cell is the same width as a
    /// regular one. Ignoring the emphasis here therefore describes the same geometry the real measure
    /// produces for a monospaced font, and keeps a snapshot grid readable.
    /// </remarks>
    public double Measure(string text, TextEmphasis emphasis)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length;
    }

    public double LineHeight => 1;

    public double IndentWidth => Indent;

    public override string ToString() => "monospace";
}
