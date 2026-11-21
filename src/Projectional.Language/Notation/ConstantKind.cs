namespace Projectional.Language;

/// <summary>What a <see cref="ConstantNotationCell"/> is made of, which is what styles key off.</summary>
public enum ConstantKind
{
    /// <summary>A word of the language, such as <c>if</c> or <c>ruleset</c>.</summary>
    Keyword,

    /// <summary>Punctuation that only groups or separates, such as <c>{</c> or <c>;</c>.</summary>
    Punctuation,

    /// <summary>
    /// Text the projection computed rather than the notation wrote - a type drawn beside its node - which a host
    /// paints as an inlay hint instead of as part of the document.
    /// </summary>
    Annotation,
}
