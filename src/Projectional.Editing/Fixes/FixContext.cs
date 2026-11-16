using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>What a fix is handed when it is asked to build the edit that repairs its finding.</summary>
/// <remarks>
/// <para>
/// The target node is resolved once, here, instead of every fix hunting for it: a fix knows the
/// diagnostic it was built for, and the session knows the project, so the lookup belongs in the seam
/// rather than in each repair.
/// </para>
/// <para>
/// <see cref="Node"/> is <see langword="null"/> when the target is no longer in the project, and that is
/// the stale case rather than an error. A finding describes a model that may have been edited since, so a
/// fix asked about a node that is gone can decline cleanly - returning <see langword="null"/> - instead
/// of throwing inside the code that is building a menu.
/// </para>
/// </remarks>
/// <param name="Model">The model the finding was reported against.</param>
/// <param name="Diagnostic">The finding this fix repairs.</param>
/// <param name="Node">The target of the finding, or <see langword="null"/> when it is gone.</param>
public readonly record struct FixContext(Model Model, Diagnostic Diagnostic, Node? Node)
{
    /// <summary>Whether the finding's target has left the project since the finding was computed.</summary>
    public bool IsStale => Node is null;
}
