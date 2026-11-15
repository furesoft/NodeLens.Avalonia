using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>One thing that is wrong, or worth pointing out, about one node of a model.</summary>
/// <remarks>
/// <para>
/// A diagnostic names a node and says where on that node to look; it never names a cell, a line or a
/// screen position. Cells and positions are derived, so a diagnostic that mentioned one would go stale
/// the moment the notation changed - and a check must be able to run on a model nobody is drawing.
/// <see cref="Role"/> and <see cref="Index"/> are the two refinements that make the reference narrow
/// enough to be useful: the slot of the node to point at, and which child of that slot.
/// </para>
/// <para>
/// Diagnostics are produced by checks - a language's own rules, a constraint runner, a loader that had
/// to keep an unknown concept - and consumed by whatever draws or lists them. Nothing here decides how
/// they look: this type is the vocabulary, a marker set collects a batch of it, and the drawing layer
/// turns it into marks. There is no callback to repair a diagnostic, no fix list and no
/// severity threshold - those belong to the thing that runs the checks.
/// </para>
/// <para>
/// A diagnostic is a snapshot of an opinion, not a fact about the model. Editing the model does not
/// update the diagnostics that were computed before the edit, so a host recomputes them after a change
/// rather than trying to patch them; one whose node is gone simply stops resolving.
/// </para>
/// </remarks>
public sealed record Diagnostic
{
    /// <summary>Describes a finding at <paramref name="target"/>.</summary>
    /// <param name="severity">How much it matters.</param>
    /// <param name="target">The node the finding is about.</param>
    /// <param name="message">What is wrong, in one sentence the user can act on.</param>
    /// <param name="role">
    /// The slot of <paramref name="target"/> to point at, or <see langword="null"/> for the node's own
    /// text. A slot that does not exist, or that the current notation hides, falls back to the node.
    /// </param>
    /// <param name="index">
    /// Which child of a many-valued <paramref name="role"/> to point at, or <see langword="null"/> for the
    /// slot as a whole. Ignored when <paramref name="role"/> is <see langword="null"/>.
    /// </param>
    /// <param name="code">
    /// The check's own identifier - <c>unresolved-reference</c>, <c>E0042</c> - for grouping, filtering and
    /// suppression. Free-form, and never shown to the user in place of <paramref name="message"/>.
    /// </param>
    /// <param name="range">
    /// The part of the value at <paramref name="role"/> the finding is about, or <see langword="null"/> for
    /// all of it. Only meaningful together with <paramref name="role"/>: without a slot there is no single
    /// value for the offsets to be counted from, so a range on a node-wide finding is ignored.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public Diagnostic(
        DiagnosticSeverity severity,
        NodeId target,
        string message,
        string? role = null,
        int? index = null,
        string? code = null,
        CharacterRange? range = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        Severity = severity;
        Target = target;
        Message = message;
        Role = role;
        Index = index;
        Code = code;
        Range = range;
    }

    /// <summary>How much this matters.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>The node this finding is about.</summary>
    public NodeId Target { get; }

    /// <summary>What is wrong, in one sentence the user can act on.</summary>
    public string Message { get; }

    /// <summary>The slot of <see cref="Target"/> to point at, or <see langword="null"/> for the node itself.</summary>
    public string? Role { get; }

    /// <summary>Which child of a many-valued <see cref="Role"/> to point at, or <see langword="null"/> for the slot.</summary>
    public int? Index { get; }

    /// <summary>The check's own identifier, for grouping and suppression, or <see langword="null"/>.</summary>
    public string? Code { get; }

    /// <summary>The part of the value at <see cref="Role"/> this finding is about, or <see langword="null"/>.</summary>
    public CharacterRange? Range { get; }
}
