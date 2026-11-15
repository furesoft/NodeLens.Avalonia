namespace Projectional.Language;

/// <summary>
/// How a piece of text is drawn beyond its colour: heavier, slanted, lined, or struck through.
/// </summary>
/// <remarks>
/// <para>
/// Emphasis is not colour. Colour is decided by the palette from what a cell already is, and can be
/// swapped without the model, the tree or the layout noticing. Emphasis cannot: two of these four
/// decorations change the width of the glyphs, so it has to be known while the tree is built and
/// carried on the cell, where the measure can take it into account before any rectangle is computed.
/// </para>
/// <para>
/// A language declares it in its notation, next to the keyword or value it applies to; a host that
/// wants a decoration to depend on what the model holds supplies a rule that is asked while the tree
/// is built. Either way the answer ends up in the same place, so rendering has one thing to look at.
/// </para>
/// <para>
/// The flags combine: <c>Bold | Italic</c> asks for a bold italic face, and
/// <c>Underline | Strikethrough</c> draws both lines. <see cref="None"/> is the absence of every
/// decoration and what every cell starts with.
/// </para>
/// </remarks>
[Flags]
public enum TextEmphasis
{
    /// <summary>No decoration at all, drawn in the font's regular face.</summary>
    None = 0,

    /// <summary>A heavier face, which is wider than the regular one and therefore changes layout.</summary>
    Bold = 1,

    /// <summary>A slanted face, which is also wider than the regular one.</summary>
    Italic = 2,

    /// <summary>A line under the text. Painted below the glyphs, so it changes no measurement.</summary>
    Underline = 4,

    /// <summary>A line through the text, the way a stricken element reads. Changes no measurement.</summary>
    Strikethrough = 8,
}
