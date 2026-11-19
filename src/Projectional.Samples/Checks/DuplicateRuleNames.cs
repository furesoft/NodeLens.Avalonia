using System.Globalization;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Samples;
using Projectional.Semantics;

namespace Projectional.Samples.Checks;

/// <summary>The rule names inside one ruleset that another rule of the same ruleset already uses.</summary>
/// <remarks>
/// <para>
/// This is the sample's own check, and it exists to prove the whole quick-fix path with nothing but a
/// model: it walks one child slot, compares sibling names, and reports the later of two rules that share
/// one. No scope resolution, no type inference, no constraint engine - the part that needs those is M4.
/// </para>
/// <para>
/// It lives in <c>Checks/</c> of <c>Projectional.Samples</c> rather than beside it, which is the shape
/// [ADR 0021](../../../docs/adr/0021-one-project-per-language.md) settled on: a check produces diagnostics and repairs,
/// so it names the editing layer, and the behaviour of a language belongs in the language's own project. The namespace
/// stayed <c>Projectional.Samples.Checks</c>, so a host that names it is unaffected.
/// </para>
/// <para>
/// Every finding it makes carries its cure - a rename to the first free suffix, one property write - so it
/// answers with fixes and lets the caller derive the markers from them. A check that found a problem it
/// could not repair would return diagnostics as well; that shape arrives with the real constraint engine.
/// </para>
/// </remarks>
public static class DuplicateRuleNames
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "RULES001";

    /// <summary>What is wrong in <paramref name="model"/>, with a repair for each finding.</summary>
    /// <param name="model">The model to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per duplicated name, in document order, targeting the name property of the rule that repeats
    /// a name so the editor draws the report under the word rather than under the whole node.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<DiagnosticFix> Of(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> findings = [];

        foreach (var root in model.Roots)
        {
            foreach (var node in SelfAndDescendants(root))
            {
                if (string.Equals(node.Concept.Key, RulesConceptKeys.Ruleset, StringComparison.Ordinal))
                {
                    CollectFrom(node, findings);
                }
            }
        }

        return findings;
    }

    /// <summary>Reports every rule of <paramref name="ruleset"/> that repeats a name of an earlier one.</summary>
    /// <remarks>
    /// The names already taken grow as proposals are made, so two rules named <c>Check</c> propose
    /// <c>Check2</c> and <c>Check3</c> rather than both proposing <c>Check2</c> - which is a repair that
    /// would only work if the user applied the first one, and a real bug in the naive version.
    /// </remarks>
    private static void CollectFrom(Node ruleset, List<DiagnosticFix> findings)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in ruleset.GetChildren(RulesLanguage.RulesSlot))
        {
            if (Named(rule) is not { } name)
            {
                continue;
            }

            if (taken.Add(name))
            {
                continue;
            }

            findings.Add(FixFor(rule, name, FirstFree(name, taken)));
        }
    }

    /// <summary>The fix that renames <paramref name="rule"/> to <paramref name="proposal"/>.</summary>
    private static DiagnosticFix FixFor(Node rule, string name, string proposal)
    {
        var diagnostic = new Diagnostic(
            DiagnosticSeverity.Warning,
            rule.Id,
            $"another rule in this ruleset is already called \"{name}\".",
            role: rule.Concept.NameSlot?.Name,
            code: Code);

        return new DiagnosticFix(
            diagnostic,
            "rules.duplicate-name.rename",
            $"Rename to \"{proposal}\"",
            context => context.Node is { } node
                ? new SetPropertyAction(CellAddress.OfSlot(node.Id, node.Concept.NameSlot!.Name), proposal)
                : null,
            "Gives the rule a name no other rule of this ruleset uses.");
    }

    /// <summary><paramref name="name"/> with the first suffix that is not in <paramref name="taken"/>.</summary>
    /// <remarks>
    /// Numbering starts at two, because <c>Check</c> itself is the name being replaced and
    /// <c>Check1</c> next to <c>Check</c> reads like a typo rather than like a second rule.
    /// </remarks>
    private static string FirstFree(string name, HashSet<string> taken)
    {
        for (var suffix = 2; ; suffix++)
        {
            var candidate = string.Concat(name, suffix.ToString(CultureInfo.InvariantCulture));

            // Add answers whether the name was free, and keeps it when it was - which is what makes the
            // second proposal of the same name move on instead of repeating the first one.
            if (taken.Add(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>The name of <paramref name="node"/> when it has one, or <see langword="null"/>.</summary>
    private static string? Named(Node node) =>
        node.Concept.NameSlot is { } slot && node.GetOrNull(slot.Name) is { Length: > 0 } name ? name : null;

    /// <summary>The node and everything below it, in document order.</summary>
    private static IEnumerable<Node> SelfAndDescendants(Node node)
    {
        yield return node;

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
