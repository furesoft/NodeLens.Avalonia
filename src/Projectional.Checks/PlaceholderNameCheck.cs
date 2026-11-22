using System.Globalization;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A name that still equals the placeholder its concept starts it with.</summary>
/// <remarks>
/// <para>
/// The placeholder is read from the concept's own declaration rather than written down here, so the check
/// stays correct when a language changes its default and never fires on a name the user chose - even one
/// that happens to read like a placeholder. A concept that declares no default is not reported: an empty
/// name is a name nobody has typed yet, not something the user was meant to replace.
/// </para>
/// <para>
/// The finding is a <see cref="DiagnosticSeverity.Hint"/> rather than a warning. An unnamed node is a
/// document in progress - a new document opens with one - and a squiggle under every fresh node teaches the
/// user to ignore squiggles.
/// </para>
/// <para>
/// Each repair names the node after its concept with the first free suffix, and the proposals are computed
/// as a group: two unnamed rules are offered <c>Rule1</c> and <c>Rule2</c>, not <c>Rule1</c> twice. The
/// numbering starts at one because <c>Rule</c> on its own reads like the concept rather than like a node of
/// it. A concept that declares a spelling rule (<see cref="Concept.NameRule"/>) has its proposal respelled by
/// that rule, so the name this fix writes is one the language accepts - a repair that left
/// <c>STD004</c>'s finding behind would be broken.
/// </para>
/// </remarks>
public sealed class PlaceholderNameCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD001";

    string ICheck.Code => Code;

    string ICheck.Title => "A name that still equals the placeholder its concept starts it with.";

    /// <summary>The stable identifier of the repair that names the node.</summary>
    public const string FixId = "std.placeholder-name.name";

    /// <summary>Every node whose name still equals its concept's declared placeholder.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per unnamed node, in document order, targeting the name property so the editor draws the
    /// report under the word rather than under the whole node.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<(NodeId Id, string Slot, string Placeholder, string Concept, NameRule? Rule)> unnamed = [];
        HashSet<string> used = new(StringComparer.Ordinal);

        foreach (var node in NodeWalk.Of(model))
        {
            if (node.Concept.NameSlot is not { DefaultValue: { Length: > 0 } placeholder } nameSlot)
            {
                continue;
            }

            var name = node.GetOrNull(nameSlot.Name);

            if (name != placeholder)
            {
                if (name is not null)
                {
                    used.Add(name);
                }

                continue;
            }

            unnamed.Add((node.Id, nameSlot.Name, placeholder, node.Concept.Key, node.Concept.NameRule));
        }

        List<DiagnosticFix> fixes = [];

        foreach (var (id, slot, placeholder, concept, rule) in unnamed)
        {
            var proposal = FirstFree(ConceptWords.Word(concept), rule, used);
            var target = CellAddress.OfSlot(id, slot);

            fixes.Add(new DiagnosticFix(
                new Diagnostic(
                    DiagnosticSeverity.Hint,
                    id,
                    $"this {ConceptWords.Describe(concept)} is still called \"{placeholder}\"; give it a name.",
                    role: slot,
                    code: Code),
                FixId,
                $"Name it \"{proposal}\"",

                // Deliberately address-based: the closure keeps an id and a slot, never the node, so the
                // repair still means the same thing after an undo or against a copy of the document.
                context => context.Node is null ? null : new SetPropertyAction(target, proposal),
                "Names the node after its concept, which is what a user would type."));
        }

        return fixes;
    }

    /// <summary><paramref name="word"/> with the first number that is not in <paramref name="used"/>.</summary>
    /// <remarks>
    /// The chosen name joins the set, so a second unnamed node of the same concept moves on rather than
    /// proposing the name the first one is already offered. <paramref name="rule"/> respells the candidate,
    /// which is why the set holds the proposal and not the plain <c>Rule1</c> - a name the language would
    /// report is not a name this fix may write.
    /// </remarks>
    private static string FirstFree(string word, NameRule? rule, HashSet<string> used)
    {
        for (var suffix = 1; ; suffix++)
        {
            var candidate = string.Concat(word, suffix.ToString(CultureInfo.InvariantCulture));

            if (rule is not null)
            {
                candidate = rule.Propose(candidate);
            }

            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
