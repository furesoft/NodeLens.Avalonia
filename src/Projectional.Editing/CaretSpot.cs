using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Where the caret stands, as the two numbers a status bar shows.
/// </summary>
/// <remarks>
/// <para>
/// A projectional document has no lines of its own: a line is a band of the layout that text is drawn in,
/// which is the definition <see cref="LineNumbers"/> already applies to the gutter. So the number reported
/// here is the number the control paints beside the caret's row, and the two cannot disagree.
/// </para>
/// <para>
/// The column counts characters from the start of the caret's own cell line, because that is the only column
/// an offset can name: several cells share a drawn row, and nothing in the model says where one of them ends
/// and the next begins. It is a position for the reader, not an address, and it is derived whenever the
/// caret moves rather than kept anywhere.
/// </para>
/// </remarks>
/// <param name="Line">The numbered row of the layout the caret is drawn on, counting from one.</param>
/// <param name="Column">How many characters of the caret's cell stand to its left, counting from one.</param>
public readonly record struct CaretSpot(int Line, int Column)
{
    /// <summary>Where the caret of <paramref name="session"/> stands, or <see langword="null"/> when it is drawn nowhere.</summary>
    /// <param name="session">The session whose caret is asked about.</param>
    /// <returns>The spot, or <see langword="null"/> when the caret's cell has no rectangle in the layout.</returns>
    /// <remarks>
    /// A caret can name a place the projection does not draw - a cell whose notation draws nothing - and then
    /// there is no line and no column to report: a made-up number would send the reader looking for a place
    /// that is not on the page.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static CaretSpot? Of(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var caret = session.Selection.Caret;

        if (CaretPlaces.PlaceAt(CaretPlaces.Of(session.Tree), caret) is not { } place
            || session.Layout.BoundsOf(place.Cell) is not { } bounds)
        {
            return null;
        }

        var lines = session.Layout.TextOf(place.Cell, session.Measure);
        var top = CaretGeometry.TopOf(bounds, lines.IndexAt(caret.Offset), session.Measure);

        return RowAt(session, top) is { } line ? new CaretSpot(line, lines.ColumnOf(caret.Offset) + 1) : null;
    }

    /// <summary>The spot as a status bar spells it.</summary>
    public override string ToString() => $"Ln {Line}, Col {Column}";

    /// <summary>The number of the row drawn at <paramref name="top"/>, or <see langword="null"/> when no row is.</summary>
    /// <remarks>
    /// The tolerance is the same half line <see cref="LineNumbers"/> uses to decide that two tops are one row,
    /// so the answer here is the answer the gutter gave for the same band of the layout.
    /// </remarks>
    private static int? RowAt(EditorSession session, double top)
    {
        var tolerance = session.Measure.LineHeight / 2;

        foreach (var row in LineNumbers.Of(session.Layout, session.Measure))
        {
            if (Math.Abs(row.Bounds.Y - top) < tolerance)
            {
                return row.Number;
            }
        }

        return null;
    }
}
