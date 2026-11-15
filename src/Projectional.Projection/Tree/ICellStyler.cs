namespace Projectional.Projection;

/// <summary>
/// Decides what a cell's text means, so that a renderer can colour it without interpreting the tree.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is <see cref="CellStyler"/>, and most hosts never look at this interface.
/// It exists because classification is a language question, not a UI one: a language that spells its
/// operators as enumeration symbols may want them to read as keywords, and one that introduces
/// annotations or comments has categories this vocabulary does not know about. Overriding it changes
/// what the reader sees without changing the model, the notation or the layout.
/// </para>
/// <para>
/// A styler must be a pure function of the cell. Layout measures text before it is styled, so a styler
/// that changed metrics - a bolder font, a wider glyph - would desynchronise the drawing from the
/// rectangles the caret and hit testing were computed from.
/// </para>
/// </remarks>
public interface ICellStyler
{
    /// <summary>What the text of <paramref name="cell"/> means.</summary>
    TextStyleKind StyleOf(EditorCell cell);
}
