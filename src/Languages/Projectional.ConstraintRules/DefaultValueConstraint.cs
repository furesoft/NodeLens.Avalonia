using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.ConstraintRules;

/// <summary>A property whose value is still the one its concept declares for it.</summary>
/// <remarks>
/// The placeholder is read from the declaration rather than written down in the rule, so it stays correct when the
/// language changes its default and never fires on a value the user chose - even one that happens to read like a
/// placeholder. A property with no declared default has no placeholder, so the rule says nothing there.
/// </remarks>
public sealed class DefaultValueConstraint(string slot, string message, DiagnosticSeverity severity, string? code = null)
    : SlotConstraint(slot, message, severity, code)
{
    private protected override bool Applies(Node node) =>
        node.Concept.FindProperty(Slot)?.DefaultValue is { Length: > 0 } value
        && string.Equals(node.GetOrNull(Slot), value, StringComparison.Ordinal);
}
