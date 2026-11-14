namespace Projectional.Core;

/// <summary>
/// Thrown when an operation would leave the model in a shape that
/// <c>docs/architecture/model.md</c> declares impossible.
/// </summary>
/// <remarks>
/// The invariants are always enforced, not only in debug builds: a violation has to be
/// observable by a test and by a host application, and an editor that aborts the process on a
/// bad drag-and-drop is not a usable component. Catching this exception and rejecting the
/// operation is the intended behaviour.
/// </remarks>
public sealed class ModelInvariantException : InvalidOperationException
{
    public ModelInvariantException()
    {
    }

    public ModelInvariantException(string message)
        : base(message)
    {
    }

    public ModelInvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
