namespace Projectional.Yaml;

/// <summary>One token of a YAML file: what it is, the text it was written as, and where it starts and ends.</summary>
/// <remarks>
/// The offsets are what make this importer honest: a scalar keeps the exact slice of the file it came from, and a raw
/// construct is a slice too, so nothing has to reconstruct a spelling. They are also what turns a refusal into a line and
/// a column.
/// </remarks>
/// <param name="Kind">What the token is.</param>
/// <param name="Text">The text exactly as the file spelled it, or the name an alias carries.</param>
/// <param name="Start">Where the token starts, as a character offset into the text a host passed.</param>
/// <param name="End">Where it ends, as the character offset just past its last character.</param>
internal readonly record struct YamlToken(YamlTokenKind Kind, string Text, int Start, int End);
