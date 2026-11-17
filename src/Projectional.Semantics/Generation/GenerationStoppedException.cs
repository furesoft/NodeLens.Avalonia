namespace Projectional.Semantics;

/// <summary>Unwinds the template walk once a failure is recorded.</summary>
/// <remarks>
/// Internal and parameterless: a template is data, so no user code can throw here - this exists only so that a
/// failure deep inside a child expansion stops the walk without every caller passing a result back up.
/// </remarks>
internal sealed class GenerationStoppedException : Exception
{
    internal GenerationStoppedException()
        : base("The generation stopped.")
    {
    }
}
