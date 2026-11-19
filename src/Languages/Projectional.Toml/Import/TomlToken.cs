namespace Projectional.Toml;

/// <summary>One token of a TOML file: what it is and the text it was written as.</summary>
/// <remarks>
/// The reader's tokens and its trivia are the same shape here on purpose. A comment, a newline and a pair are all
/// "something in the file, in order", and the parser has to see them in that order - so making trivia a second kind of
/// thing would give the parser two sequences to merge and a way to get the order wrong.
/// </remarks>
/// <param name="Kind">What the token is.</param>
/// <param name="Text">The text exactly as the file spelled it.</param>
/// <param name="Offset">Where the token starts, as a character offset into the text a host passed.</param>
internal readonly record struct TomlToken(Tomlyn.Syntax.TokenKind Kind, string Text, int Offset);
