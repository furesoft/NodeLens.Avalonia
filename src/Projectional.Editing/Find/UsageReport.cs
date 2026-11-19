using Projectional.Core;

namespace Projectional.Editing;

/// <summary>The answer to one find-usages question: the node it asked about, and who points at it.</summary>
/// <param name="Target">The node the caret names, or <see langword="null"/> when it names none.</param>
/// <param name="Usages">Every reference to <paramref name="Target"/>, in document order; empty when it is none.</param>
/// <remarks>
/// A report is a snapshot of the project at the moment the gesture was pressed, like a run's output: an edit can
/// retarget or remove a reference, so the next question has to be asked again rather than patched.
/// </remarks>
public sealed record UsageReport(Node? Target, IReadOnlyList<UsageMatch> Usages)
{
    /// <summary>The answer when the caret names no node at all.</summary>
    public static UsageReport None { get; } = new(null, []);

    /// <summary>Whether the question had something to ask about.</summary>
    /// <remarks>
    /// "No target" and "a target nothing points at" are different answers: the first means the caret is somewhere a
    /// usage cannot be asked about, the second means the document is clean of references to it.
    /// </remarks>
    public bool HasTarget => Target is not null;
}
