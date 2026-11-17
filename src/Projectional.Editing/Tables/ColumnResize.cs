using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One column boundary a reader can drag, and the width that column has right now.
/// </summary>
/// <param name="Owner">The node that owns the table.</param>
/// <param name="Slot">The child slot the table draws.</param>
/// <param name="Column">The column's index, left to right.</param>
/// <param name="Width">How wide the column is at the moment, in layout units.</param>
/// <param name="Boundary">The x of the column's right edge, which is what the pointer grabs.</param>
/// <param name="Top">The top of the header row, so a host can draw a handle there.</param>
/// <param name="Bottom">The bottom of the header row.</param>
public readonly record struct ColumnHandle(
    NodeId Owner,
    string Slot,
    int Column,
    double Width,
    double Boundary,
    double Top,
    double Bottom);

/// <summary>
/// Finds the table column boundary under a point, which is the whole of "resize a column" that is not
/// drawing.
/// </summary>
/// <remarks>
/// <para>
/// The rule lives here rather than in the control for the same reason every other editing rule does: it
/// is a decision about the document, it can be tested headlessly, and a host that draws its own surface
/// gets it for free. The control only turns a drag into <see cref="EditorSession.SetColumnWidth"/>.
/// </para>
/// <para>
/// A handle is the <em>right</em> edge of a column, and only inside the header row: the body of a table
/// is where the reader selects and types, and a boundary that could be grabbed anywhere would make
/// clicking the last character of a value start a drag instead of putting the caret there.
/// </para>
/// </remarks>
public static class ColumnResize
{
    /// <summary>
    /// The column boundary within <paramref name="tolerance"/> of the point, or <see langword="null"/>
    /// when the point is not on one.
    /// </summary>
    /// <param name="session">The session whose layout is being pointed at.</param>
    /// <param name="x">The pointer's x, in layout units.</param>
    /// <param name="y">The pointer's y, in layout units.</param>
    /// <param name="tolerance">How far from the boundary, horizontally, still counts as on it.</param>
    /// <remarks>
    /// The tolerance is horizontal only. A vertical one would be a whole line in a monospaced measure,
    /// which would make the first row of the body a handle - and the body is where the reader selects
    /// and types.
    /// </remarks>
    public static ColumnHandle? HandleAt(EditorSession session, double x, double y, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        var gap = session.Measure.Measure(" ", TextEmphasis.None);

        foreach (var table in session.Tree.Cells().OfType<TableCell>())
        {
            if (table.ColumnCount == 0)
            {
                continue;
            }

            var top = double.MaxValue;
            var bottom = double.MinValue;

            foreach (var header in table.Headers)
            {
                if (session.Layout.BoundsOf(header) is not { } bounds)
                {
                    continue;
                }

                top = Math.Min(top, bounds.Y);
                bottom = Math.Max(bottom, bounds.Bottom);
            }

            if (top > bottom || y < top || y >= bottom)
            {
                continue;
            }

            for (var column = 0; column < table.ColumnCount; column++)
            {
                if (session.Layout.BoundsOf(table.Headers[column]) is not { } header)
                {
                    continue;
                }

                var left = header.X;
                var width = column + 1 < table.ColumnCount
                    ? session.Layout.BoundsOf(table.Headers[column + 1]) is { } next
                        ? next.X - left - gap
                        : header.Width
                    : session.Layout.BoundsOf(table) is { } box
                        ? box.Right - left
                        : header.Width;

                var boundary = left + width;

                if (Math.Abs(x - boundary) <= tolerance)
                {
                    return new ColumnHandle(
                        table.Address.Owner,
                        table.Slot,
                        column,
                        width,
                        boundary,
                        top,
                        bottom);
                }
            }
        }

        return null;
    }
}
