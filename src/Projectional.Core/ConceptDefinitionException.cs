namespace Projectional.Core;

/// <summary>
/// Thrown when a set of concept declarations does not describe a usable language: an unknown
/// super concept, an inheritance cycle, conflicting slot declarations, or a slot whose target
/// concept is never declared.
/// </summary>
public sealed class ConceptDefinitionException : Exception
{
    public ConceptDefinitionException()
    {
    }

    public ConceptDefinitionException(string message)
        : base(message)
    {
    }

    public ConceptDefinitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
