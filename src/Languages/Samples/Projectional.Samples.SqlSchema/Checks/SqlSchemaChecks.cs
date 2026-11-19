using Projectional.Checks;

namespace Projectional.Samples.SqlSchema.Checks;

/// <summary>The checks a host runs on a schema: the standard catalogue plus the one only this language has.</summary>
public static class SqlSchemaChecks
{
    /// <summary>Every check this language offers, in the order their findings are shown.</summary>
    public static CheckSet All { get; } = StandardChecks.All.With(new ColumnNamesCheck());
}
