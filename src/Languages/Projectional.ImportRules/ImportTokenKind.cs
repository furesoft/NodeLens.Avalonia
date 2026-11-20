namespace Projectional.ImportRules;

/// <summary>The kinds of lexical class a capture may read.</summary>
/// <remarks>
/// Four, and the closed set is the point: a word, a number, a quoted run, and text that produces no node. A fifth
/// kind would be a lexer written in a model, which is the interpreter this framework keeps refusing.
/// <see cref="Skipped"/> is not read by a capture at all - the reader steps over it wherever it looks for the next
/// part, which is what makes whitespace and comments disappear from a rule's text.
/// </remarks>
public enum ImportTokenKind
{
    /// <summary>Letters, digits, underscore and the declared extras.</summary>
    Word,

    /// <summary>An optional sign, digits, an optional fraction and an optional exponent.</summary>
    Number,

    /// <summary>A run between two marks, with escapes resolved.</summary>
    Quoted,

    /// <summary>Whitespace or a comment: text no rule sees.</summary>
    Skipped,
}
