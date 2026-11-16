using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A name with whitespace in front of or behind it.</summary>
/// <remarks>
/// <para>
/// Whitespace in a name is invisible in the editor and significant in the model: two rules called
/// <c>"Check"</c> and <c>"Check "</c> are different names, a reference written against one does not resolve
/// against the other, and nothing on screen explains why. Trimming is therefore a repair that cannot change
/// what the user meant - only what they could not see.
/// </para>
/// <para>
/// A name that is nothing but whitespace is not reported: trimming it would produce the empty name, and an
/// empty name is a different question (the concept's placeholder, which <see cref="PlaceholderNameCheck"/>
/// answers when the language declares one) rather than a trim.
/// </para>
/// </remarks>
public sealed class SurroundingWhitespaceCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD003";

    /// <summary>The stable identifier of the repair that trims the name.</summary>
    public const string FixId = "std.surrounding-whitespace.trim";

    /// <summary>Every node whose name has leading or trailing whitespace.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per affected name, in document order, targeting the name property so the marker lands under
    /// the text rather than under the node.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var node in NodeWalk.Of(model))
        {
            if (node.Concept.NameSlot is not { } nameSlot || node.GetOrNull(nameSlot.Name) is not { } name)
            {
                continue;
            }

            var trimmed = name.Trim();

            if (trimmed.Length == 0 || string.Equals(trimmed, name, StringComparison.Ordinal))
            {
                continue;
            }

            var target = CellAddress.OfSlot(node.Id, nameSlot.Name);

            fixes.Add(new DiagnosticFix(
                new Diagnostic(
                    DiagnosticSeverity.Warning,
                    node.Id,
                    "the name starts or ends with whitespace, which reads as part of the name.",
                    role: nameSlot.Name,
                    code: Code),
                FixId,
                $"Trim to \"{trimmed}\"",
                context => context.Node is null ? null : new SetPropertyAction(target, trimmed),
                "Removes the whitespace the user cannot see, leaving the name itself unchanged."));
        }

        return fixes;
    }
}
