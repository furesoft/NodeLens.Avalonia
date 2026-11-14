using Projectional.Core;

namespace Projectional.Persistence;

/// <summary>
/// Thrown when a stored model cannot be turned into a model tree: the document is not
/// well-formed, uses an element the format does not define, or carries a value the language
/// refuses.
/// </summary>
/// <remarks>
/// This is the failure of last resort. A file that is merely out of date with the language — a
/// concept that was renamed, a reference whose target is gone — is not an error in this sense;
/// those cases keep their data and are reported as diagnostics once loading produces them.
/// </remarks>
public sealed class ModelFormatException : Exception
{
    public ModelFormatException()
    {
    }

    public ModelFormatException(string message)
        : base(message)
    {
    }

    public ModelFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
