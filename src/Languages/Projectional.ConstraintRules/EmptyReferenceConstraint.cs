using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.ConstraintRules;

/// <summary>A reference that points at nothing.</summary>
/// <remarks>
/// One rule for both states, because a reader cannot tell them apart and the intent is the same: a reference the
/// language requires is missing whether it was never set or its target was deleted. A reference that still resolves
/// is never reported - what a stale relation means is the ambiguity constraint's question, not this one's.
/// </remarks>
public sealed class EmptyReferenceConstraint(string slot, string message, DiagnosticSeverity severity, string? code = null)
    : SlotConstraint(slot, message, severity, code)
{
    private protected override bool Applies(Node node) =>
        node.Concept.FindReference(Slot) is not null && node.GetReferenceTarget(Slot) is null;
}
