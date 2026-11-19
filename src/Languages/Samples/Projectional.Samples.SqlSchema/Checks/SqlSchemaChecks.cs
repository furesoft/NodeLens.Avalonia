using Projectional.Checks;

namespace Projectional.Samples.SqlSchema.Checks;

/// <summary>The checks a host runs on a schema: the standard catalogue plus the one only this language has.</summary>
/// <remarks>
/// <b>The reference group is left out on purpose.</b> A key names a column of another <em>document</em> - that is
/// the axis this sample exists to prove - and a schema is several documents, of which a user opens one. Every key
/// into a document that is not open would therefore read as a dangling reference, which is what
/// <c>STD020</c>/<c>STD021</c> report. Leaving the group out says that the language means it, rather than
/// switching a check off per document.
/// </remarks>
public static class SqlSchemaChecks
{
    /// <summary>Every check this language offers, in the order their findings are shown.</summary>
    public static CheckSet All { get; } = new CheckSet(
    [
        .. StandardChecks.Names,
        .. StandardChecks.Structure,
        .. StandardChecks.Expressions,
        new ColumnNamesCheck(),
    ]);
}