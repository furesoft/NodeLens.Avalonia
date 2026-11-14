namespace Projectional.Projection;

/// <summary>The projection cannot be built from the current model and notation.</summary>
/// <remarks>
/// This reports a broken combination of model, concept and notation, not a user mistake: a user typing
/// nonsense produces validation messages, while the projection failing means the DSL author wrote
/// something that no screen could show. It is thrown only where continuing would leave the tree
/// inconsistent, such as a cell that belongs in two places.
/// </remarks>
public sealed class ProjectionException : Exception
{
    public ProjectionException(string message)
        : base(message)
    {
    }

    public ProjectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ProjectionException()
    {
    }
}
