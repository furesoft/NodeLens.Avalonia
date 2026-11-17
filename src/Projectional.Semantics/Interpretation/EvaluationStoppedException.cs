namespace Projectional.Semantics;

/// <summary>Stops a run that must not continue, because its budget ran out or the caller cancelled.</summary>
/// <remarks>
/// A private sentinel between the runtime and itself: <see cref="Interpreter"/> sets the failure it will report
/// and throws this, so a rule's own <c>catch</c> cannot mistake a budget stop for a model problem and neither
/// needs to know the other exists.
/// </remarks>
internal sealed class EvaluationStoppedException : Exception
{
    internal EvaluationStoppedException()
        : base("The interpretation was stopped.")
    {
    }
}
