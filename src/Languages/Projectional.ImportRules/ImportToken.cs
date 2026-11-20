namespace Projectional.ImportRules;

/// <summary>One declared lexical class.</summary>
/// <param name="Name">The name rules refer to it by.</param>
/// <param name="Kind">Which of the four classes it is.</param>
/// <param name="Extra">The characters a word accepts beyond letters, digits and underscore.</param>
/// <param name="Quote">The mark a quoted run stands between, or empty.</param>
/// <param name="Escape">The character that makes the next one literal, or empty to resolve none.</param>
/// <param name="Prefix">The text that starts a comment, or empty to skip whitespace.</param>
/// <param name="Until">What ends a comment, or empty to run to the end of the input.</param>
/// <remarks>
/// One record for four classes for the reason <see cref="ImportPart"/> gives: the author writes a concept, and what
/// the reader walks is one shape. The unused fields are empty rather than absent, which is what a declared class with
/// three properties already is.
/// </remarks>
public sealed record ImportToken(
    string Name,
    ImportTokenKind Kind,
    string Extra = "",
    string Quote = "\"",
    string Escape = "\\",
    string Prefix = "",
    string Until = "\\n")
{
    public override string ToString() => Kind switch
    {
        ImportTokenKind.Word => $"word {Name}",
        ImportTokenKind.Number => $"number {Name}",
        ImportTokenKind.Quoted => $"quoted {Name}",
        _ => $"skipped {Name}",
    };
}
