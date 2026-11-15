using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// A cell that shows a string and arranges nothing.
/// </summary>
/// <remarks>
/// <para>
/// Every cell the reader actually sees a character in derives from this: keywords and punctuation,
/// property values, reference names, spaces and the marker for a concept without notation. Layout and
/// rendering ask for <see cref="Text"/> and are done, instead of switching over five cell kinds whose
/// only difference is where the string came from.
/// </para>
/// <para>
/// The text is fixed at construction because a cell belongs to one model version. Whatever the string
/// is - a keyword, a stored value, a stand-in for a missing value - it was decided while the tree was
/// built, so nothing downstream has to reach back into the model to find out what to draw.
/// </para>
/// </remarks>
public abstract class TextCell : EditorCell
{
    private protected TextCell(CellAddress address, string text, TextEmphasis emphasis = TextEmphasis.None)
        : base(address)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Emphasis = emphasis;
    }

    /// <summary>What the reader sees. May be empty when the slot holds no value yet.</summary>
    public string Text { get; }

    /// <summary>
    /// How the text is decorated - bold, italic, underlined, struck through, or a combination.
    /// </summary>
    /// <remarks>
    /// It lives on the cell rather than being decided while painting because two of the decorations
    /// change the width of the glyphs. Everything that turns text into a rectangle - the layout, the
    /// caret, the hit test, the selection - measures through <see cref="ITextMeasure"/> with this
    /// value, so the boxes and the glyphs inside them agree. A decoration invented at paint time
    /// would be drawn inside a box measured for a different face.
    /// </remarks>
    public TextEmphasis Emphasis { get; }
}
