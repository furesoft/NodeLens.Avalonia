using System.Globalization;
using System.Text;

using Projectional.Checks;
using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Core.Tests;

/// <summary>
/// The three comparisons the parity rule is made of: a registry dump, a snapshot and a set of findings.
/// </summary>
/// <remarks>
/// <para>
/// [ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md) says two authoring paths stay at the
/// same standing, and a rule nobody executes is a wish. This is the executable form: each method makes one aspect
/// of a language comparable as text, so a difference is a diff a reviewer can read instead of a boolean.
/// </para>
/// <para>
/// Ordering is part of every answer on purpose. Declaration order decides what an empty slot is filled with and
/// which completion entry comes first, so a comparison that used sets would miss exactly the defects that change
/// what a user sees.
/// </para>
/// </remarks>
internal static class ParityHarness
{
    /// <summary>A language's declarations as one ordered, printable block.</summary>
    public static string Dump(ConceptRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var text = new StringBuilder();

        foreach (var concept in registry.Concepts)
        {
            text.Append(concept.Key)
                .Append(concept.IsAbstract ? " abstract" : string.Empty)
                .Append(concept.IsOfferedInCompletion ? string.Empty : " hidden")
                .Append(" name=").Append(concept.NameSlot?.Name ?? "-")
                .Append(" collapse=").Append(concept.CollapseSlot?.Name ?? "-")
                .Append(" description=").Append(concept.Description ?? "-")
                .Append(" supers=").AppendJoin('>', concept.SuperConcepts.Select(super => super.Key))
                .AppendLine();

            foreach (var slot in concept.Slots)
            {
                text.Append("  ").Append(slot.GetType().Name).Append(' ').Append(slot.Name);

                switch (slot)
                {
                    case PropertySlot property:
                        text.Append(": ").Append(property.Type.Name)
                            .Append(" default=").Append(property.DefaultValue ?? "-")
                            .Append(" description=").Append(property.Description ?? "-");
                        break;

                    case ChildSlot child:
                        text.Append(": ").Append(child.ChildConceptKey)
                            .Append(' ').Append(child.Cardinality.ToString())
                            .Append(" min=").Append(child.Minimum);
                        break;

                    case ReferenceSlot reference:
                        text.Append(": ").Append(reference.TargetConceptKey)
                            .Append(reference.AllowOwnSubtree ? " own" : string.Empty);
                        break;

                    default:
                        break;
                }

                text.AppendLine();
            }
        }

        return text.ToString();
    }

    /// <summary>One document as one drawing, which is what a user would see.</summary>
    public static string Render(Node document, NotationRegistry notations, int width = 80)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(notations);

        return LayoutSnapshot.Render(document, notations, width);
    }

    /// <summary>The findings of a check set, as one ordered, printable block.</summary>
    /// <remarks>
    /// The severity, the code, the target, the role and the range are the whole of what a finding promises, so all
    /// of them are compared; the message is not, because two languages may word the same defect differently and
    /// the promise is about what was found, not about the sentence.
    /// </remarks>
    public static string Findings(CheckSet checks, Model model)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(model);

        var text = new StringBuilder();

        // The target is compared by its place in the document, not by its id: two documents built the same way
        // have different node identities, and the promise a finding makes is about *where* it is.
        var order = model.Nodes.Select((node, index) => (node.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);

        foreach (var fix in checks.Find(model))
        {
            var finding = fix.Diagnostic;

            text.Append(finding.Severity)
                .Append(' ').Append(finding.Code ?? "-")
                .Append(" at ").Append(order.TryGetValue(finding.Target, out var position) ? $"#{position}" : "?")
                .Append(" role=").Append(finding.Role ?? "-")
                .Append(" index=").Append(finding.Index?.ToString(CultureInfo.InvariantCulture) ?? "-")
                .Append(" range=").Append(finding.Range?.ToString() ?? "-")
                .AppendLine();
        }

        return text.ToString();
    }
}
