namespace Projectional.Web;

/// <summary>
/// How one layout unit becomes a CSS pixel in the browser.
/// </summary>
/// <remarks>
/// <para>
/// The projection layer measures in whatever unit <see cref="Projection.ITextMeasure"/> says. The browser sample
/// uses the monospace measure, so one unit is one character column and one line is one unit high; this record is
/// the single place that turns those columns into pixels, which is what lets the painted spans, the caret, the hit
/// test and the selection all be derived from the same numbers.
/// </para>
/// <para>
/// The values come from the page rather than from a constant: the width of a character is a property of the font
/// the browser actually chose, and a hard-coded 9.6 px would drift from the glyphs on every machine whose
/// monospace face differs. A page measures once and re-measures when the zoom or the font changes.
/// </para>
/// </remarks>
public readonly record struct CellMetrics
{
    /// <summary>The fallback used before the page has measured its font.</summary>
    public static CellMetrics Default { get; } = new(9.6, 20);

    /// <summary>Creates metrics from one cell's width and height in CSS pixels.</summary>
    /// <param name="cellWidth">The advance of one character column.</param>
    /// <param name="cellHeight">The height of one line.</param>
    public CellMetrics(double cellWidth, double cellHeight)
    {
        CellWidth = cellWidth > 0 ? cellWidth : Default.CellWidth;
        CellHeight = cellHeight > 0 ? cellHeight : Default.CellHeight;
    }

    /// <summary>The advance of one character column, in CSS pixels.</summary>
    public double CellWidth { get; }

    /// <summary>The height of one line, in CSS pixels.</summary>
    public double CellHeight { get; }

    /// <summary>How many CSS pixels <paramref name="units"/> columns take.</summary>
    public double X(double units) => units * CellWidth;

    /// <summary>How many CSS pixels <paramref name="units"/> lines take.</summary>
    public double Y(double units) => units * CellHeight;

    public override string ToString() => $"{CellWidth:0.###} x {CellHeight:0.###} px";
}
