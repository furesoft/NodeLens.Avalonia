namespace Projectional.Hocon;

/// <summary>A place where the text is not HOCON, carrying the offset that makes it a line and a column.</summary>
/// <remarks>
/// A private exception rather than a public one: it never leaves <see cref="HoconImporter"/>, which turns it into an
/// <see cref="ImportDiagnostic"/>. What it adds over a plain <see cref="FormatException"/> is the offset, because the
/// lexer and the parser both stop at a character and the contract promises a place in the text.
/// </remarks>
internal sealed class HoconSyntaxException : Exception
{
    internal HoconSyntaxException(string message, int offset)
        : base(message) => Offset = offset;

    /// <summary>Where the problem is, as a character offset into the text a host passed.</summary>
    internal int Offset { get; }
}
