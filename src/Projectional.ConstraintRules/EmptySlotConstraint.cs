using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.ConstraintRules;

/// <summary>A single-child slot that holds nothing.</summary>
/// <remarks>
/// The declared twin of <c>STD010</c>, with one deliberate difference: the catalogue's check stays silent when the
/// slot's concept has nothing concrete to instantiate, because its repair would have nothing to insert. A declared
/// rule has no repair at all, so it reports - an empty slot that promises one child is a hole either way, and the
/// author of the rule is the one who decides whether it matters.
/// </remarks>
public sealed class EmptySlotConstraint(string slot, string message, DiagnosticSeverity severity, string? code = null)
    : SlotConstraint(slot, message, severity, code)
{
    private protected override bool Applies(Node node) =>
        node.Concept.FindChild(Slot) is { Cardinality: Cardinality.One } && node.GetChildren(Slot).Count == 0;
}
