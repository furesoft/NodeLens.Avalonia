namespace Projectional.Benchmarks;

/// <summary>What one document of one scenario cost, per phase.</summary>
/// <remarks>
/// The phases are separate on purpose. A host that rechecks a document after every keystroke pays layout,
/// constraints and checks together, but they fail for different reasons and are fixed in different layers: layout is
/// the projection, constraints are the language, checks are the catalogue. A single number would say that editing a
/// document got slower without saying which of the three did.
/// </remarks>
internal sealed class Measurement
{
    /// <summary>The document size in the scenario's own unit.</summary>
    public required int Units { get; init; }

    /// <summary>How many nodes the built document holds, which is what makes two scenarios comparable.</summary>
    public required int Nodes { get; init; }

    /// <summary>Milliseconds to build the editor tree - the projection's cells - for the whole document.</summary>
    public required double Tree { get; init; }

    /// <summary>Milliseconds to measure and place a tree that is already built.</summary>
    /// <remarks>
    /// Separate from <see cref="Tree"/> because the two are different work in different layers: the tree asks the
    /// notation registry and the model, layout only asks the text measure. A keystroke pays both, and the number that
    /// says which of them is the one to attack is this pair rather than their sum.
    /// </remarks>
    public required double Arrange { get; init; }

    /// <summary>Milliseconds to build tree and layout for the whole document.</summary>
    public required double Layout { get; init; }

    /// <summary>Milliseconds to run the constraints over the whole document.</summary>
    public required double Constraints { get; init; }

    /// <summary>How many findings the constraints answered, which is why two runs are comparable at all.</summary>
    public required int Findings { get; init; }

    /// <summary>Milliseconds to run the checks over the whole document.</summary>
    public required double Checks { get; init; }

    /// <summary>How many fixes the checks answered.</summary>
    public required int Fixes { get; init; }

    /// <summary>Milliseconds to run both, which is what a change actually costs a host.</summary>
    public required double Together { get; init; }

    /// <summary>Milliseconds to recheck one unit of the document - the incremental seam's cost.</summary>
    public required double Subtree { get; init; }

    /// <summary>Milliseconds to build the tree again after a change to one node - the projection's share of a keystroke.</summary>
    /// <remarks>
    /// The counterpart of <see cref="Tree"/>: the same document, rebuilt after a change to one node, with the
    /// projections the change did not invalidate reused. The two together are the answer to "did the cache help", and
    /// the ratio between them is what a language with references gives up, because a subtree that shows one is never
    /// reused. It is not the end-to-end cost of a keystroke: the session also resolves the caret's position after a
    /// rebuild, and that work is not the projection's.
    /// </remarks>
    public required double Rebuild { get; init; }
}
