namespace Projectional.Hocon;

/// <summary>One token of a HOCON file: what it is, the text it was written as, and where it started.</summary>
/// <remarks>
/// The text is the raw slice of the file - quotes and marker included - because that is what a template has to emit
/// again. Splitting a quoted string from its delimiters, or a note from its marker, is the importer's business: it knows
/// which concept it is building.
/// </remarks>
/// <param name="Kind">What the token is.</param>
/// <param name="Text">The text exactly as the file spelled it.</param>
/// <param name="Offset">Where the token starts, as a character offset into the text a host passed.</param>
internal readonly record struct HoconToken(HoconTokenKind Kind, string Text, int Offset);
