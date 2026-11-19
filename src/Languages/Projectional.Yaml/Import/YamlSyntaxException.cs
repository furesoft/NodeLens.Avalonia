namespace Projectional.Yaml;

/// <summary>A place where the tokens do not assemble into this language, carrying the offset that makes it a line and a
/// column.</summary>
/// <remarks>
/// A private exception rather than a public one: it never leaves <see cref="YamlImporter"/>, which turns it into an
/// <see cref="Projectional.Language.ImportDiagnostic"/>. What it adds over a plain <see cref="FormatException"/> is the
/// offset, because the assembler stops at a token and the contract promises a place in the text.
/// </remarks>
internal sealed class YamlSyntaxException : Exception
{
    internal YamlSyntaxException(string message, int offset)
        : base(message) => Offset = offset;

    /// <summary>Where the problem is, as a character offset into the text a host passed.</summary>
    internal int Offset { get; }
}
