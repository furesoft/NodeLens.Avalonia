using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.ConstraintRules;

/// <summary>A value with whitespace in front of or behind it.</summary>
/// <remarks>
/// Whitespace is invisible in the editor and significant in the model: two nodes called <c>"Check"</c> and
/// <c>"Check "</c> are different names, and nothing on screen explains why. A value that is nothing but whitespace is
/// not reported, because that is the empty value rather than a value with a stray space.
/// </remarks>
public sealed class SurroundingWhitespaceConstraint(string slot, string message, DiagnosticSeverity severity, string? code = null)
    : SlotConstraint(slot, message, severity, code)
{
    private protected override bool Applies(Node node)
    {
        if (node.Concept.FindProperty(Slot) is null || node.GetOrNull(Slot) is not { } value)
        {
            return false;
        }

        var trimmed = value.Trim();

        return trimmed.Length > 0 && !string.Equals(trimmed, value, StringComparison.Ordinal);
    }
}
