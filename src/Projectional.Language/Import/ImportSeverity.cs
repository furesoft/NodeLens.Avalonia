namespace Projectional.Language;

/// <summary>How bad an import diagnostic is.</summary>
/// <remarks>
/// Two members and no third, because import has exactly two outcomes that a model can be in: the text did not parse and
/// nothing was imported, or the text parsed and a construct could not be modelled and was kept as raw text. A severity
/// ladder with "info" or "fatal" would add rungs nobody can act on differently.
/// </remarks>
public enum ImportSeverity
{
    /// <summary>The import was refused; the model is unchanged.</summary>
    Error,

    /// <summary>The import succeeded, and something in the text has no concept.</summary>
    Warning,
}
