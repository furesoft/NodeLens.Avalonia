using Projectional.Core;

namespace Projectional.Projection;

/// <summary>
/// How wide the reader has made a table's columns, which is view state rather than model state.
/// </summary>
/// <remarks>
/// <para>
/// A column width is not part of the document: it is not saved, it creates no undo step, and two
/// readers of the same model may disagree about it. It therefore reaches layout the same way the
/// measure does - as a question layout asks - instead of being written into the notation or the model.
/// </para>
/// <para>
/// The answer is a <em>minimum</em>, not an exact width. A column narrower than its content would
/// either clip a value or overlap the next column, and a projectional editor never hides what the
/// model says; a reader who drags a column narrower than its widest cell gets the widest cell.
/// </para>
/// </remarks>
public interface IColumnWidths
{
    /// <summary>
    /// The width the reader asked for, or <see langword="null"/> when they asked for nothing and the
    /// column should be as wide as its content.
    /// </summary>
    /// <param name="owner">The node that owns the table.</param>
    /// <param name="slot">The child slot the table draws.</param>
    /// <param name="column">The column's index, left to right.</param>
    double? WidthOf(NodeId owner, string slot, int column);
}
