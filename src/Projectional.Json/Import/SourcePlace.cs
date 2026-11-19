namespace Projectional.Json;

/// <summary>Where something sits in the text an import was given, as an editor spells a place.</summary>
/// <remarks>
/// Both members are 1-based, because the value exists to be shown to a user and to be handed to a host that wants to
/// put a caret in their file. The column counts characters, not bytes: the tokenizer reports a <em>byte</em> column,
/// and a file with a non-ASCII character in it would otherwise point one place too far to the right.
/// </remarks>
/// <param name="Line">The 1-based line.</param>
/// <param name="Column">The 1-based column, counted in characters.</param>
internal readonly record struct SourcePlace(int Line, int Column);
