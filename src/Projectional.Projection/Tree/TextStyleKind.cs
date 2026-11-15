namespace Projectional.Projection;

/// <summary>
/// What a piece of projected text means, which is what a renderer turns into a colour.
/// </summary>
/// <remarks>
/// <para>
/// The vocabulary stops at meaning. A cell is an <em>identifier</em>, never "green"; the host's palette
/// decides what an identifier looks like, and the same document therefore reads correctly on a light
/// background and on a dark one without the model or the notation knowing that either exists.
/// </para>
/// <para>
/// Highlighting is derived from the projection, not stored next to the text. A keyword is a keyword
/// because the notation wrote <see cref="ConstantKind.Keyword"/> there, so a model file carries no
/// formatting and cannot go out of sync with the concept that produced it - the same reason there is no
/// text buffer to re-parse.
/// </para>
/// </remarks>
public enum TextStyleKind
{
    /// <summary>Text with no claim on the reader's attention: spaces, and anything not classified.</summary>
    Default,

    /// <summary>A word of the language, such as <c>ruleset</c> or <c>if</c>.</summary>
    Keyword,

    /// <summary>Punctuation that only groups or separates, such as <c>{</c>, <c>(</c> or <c>;</c>.</summary>
    Punctuation,

    /// <summary>
    /// A name, such as a symbol like <c>+</c> or a word the language defines elsewhere.
    /// </summary>
    /// <remarks>
    /// The built-in <see cref="CellStyler"/> never picks this, because nothing a cell exposes says "this
    /// is a name": a stored string is <see cref="Text"/>, a number is <see cref="Number"/>, and an
    /// enumeration symbol is one of a closed set of words, which reads as <see cref="Keyword"/>. It is
    /// here for the host that does know, and for the palettes that are expected to be complete.
    /// </remarks>
    Identifier,

    /// <summary>A literal numeric value, including dates.</summary>
    Number,

    /// <summary>A literal text value.</summary>
    Text,

    /// <summary>The name of a reference target, which is a link to somewhere else.</summary>
    Reference,

    /// <summary>
    /// A reference whose stored target is not in the project, which is a defect the reader can repair.
    /// </summary>
    Unresolved,

    /// <summary>
    /// An empty slot's stand-in text, such as <c>&lt;condition&gt;</c>: not a value, an invitation.
    /// </summary>
    Placeholder,

    /// <summary>
    /// The marker for a concept with no notation, which is a fault in the language rather than the model.
    /// </summary>
    Missing,
}
