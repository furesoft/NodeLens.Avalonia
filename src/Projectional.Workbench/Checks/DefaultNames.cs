using System.Globalization;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Workbench;

/// <summary>Finds the nodes that are still called the name their concept starts them with.</summary>
/// <remarks>
/// <para>
/// This is the app's own check rather than the language's, and that is the point of it: a host publishes
/// findings by handing the control a <c>MarkerSet</c>, so the shell has to be able to produce one without
/// the model knowing anything about checks. The rules language's own check lives beside it in
/// <c>Projectional.Samples.Checks</c>; this one needs no language at all.
/// </para>
/// <para>
/// It is a hint rather than a warning because an unnamed node is a document in progress, not a defect: a
/// new document opens with exactly one and the user is about to type over it. A <c>Warning</c> here would
/// put a squiggle under every fresh document, which teaches the user to ignore squiggles.
/// </para>
/// <para>
/// The placeholder is read from the concept's own declaration instead of being written down here, so this
/// stays correct when a language changes its default and it never fires on a name the user chose - even
/// one that happens to read like a placeholder.
/// </para>
/// <para>
/// Each finding carries its repair: name the node after its concept, which is what a user would type. That
/// is also the demonstration the standalone document promises - a host with no internals and no editing
/// knowledge beyond the public action type can supply a diagnostic <em>and</em> its cure.
/// </para>
/// <para>
/// The proposal is respelled by the concept's own <see cref="Concept.NameRule"/> when it declares one, exactly
/// as the catalogue's <c>STD001</c> does. A host check runs for documents whose language the shell does not
/// know, so without this it would offer a name that language's own <c>STD004</c> immediately reports - a
/// repair into a finding.
/// </para>
/// </remarks>
internal static class DefaultNames
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    internal const string Code = "SHELL001";

    /// <summary>What is still unnamed in <paramref name="model"/>, with a repair for each finding.</summary>
    /// <param name="model">The model to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per unnamed node, in document order, targeting the name property that carries the
    /// placeholder so the editor can draw it under the word rather than under the whole node.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyList<DiagnosticFix> Of(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<(NodeId Id, string Slot, string Placeholder, string Word, string Concept, NameRule? Rule)> unnamed = [];
        HashSet<string> used = new(StringComparer.Ordinal);

        foreach (var root in model.Roots)
        {
            foreach (var node in SelfAndDescendants(root))
            {
                // A concept without a name slot is not "unnamed", it is anonymous by design - a literal,
                // an operator. Only a declared placeholder is something the user was meant to replace.
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

                unnamed.Add((
                    node.Id,
                    nameSlot.Name,
                    placeholder,
                    Word(node.Concept.Key),
                    Describe(node.Concept.Key),
                    node.Concept.NameRule));
            }
        }

        List<DiagnosticFix> fixes = [];

        foreach (var (id, slot, placeholder, word, concept, rule) in unnamed)
        {
            var proposal = FirstFree(word, rule, used);

            fixes.Add(new DiagnosticFix(
                new Diagnostic(
                    DiagnosticSeverity.Hint,
                    id,
                    $"this {concept} is still called \"{placeholder}\"; give it a name.",
                    role: slot,
                    code: Code),
                "shell.placeholder-name.name",
                $"Name it \"{proposal}\"",

                // Deliberately address-based: the closure keeps an id and a slot, never the node, so the
                // repair still means the same thing after an undo or against a copy of the document.
                context => context.Node is null
                    ? null
                    : new SetPropertyAction(CellAddress.OfSlot(id, slot), proposal),
                "Names the node after its concept, which is what a user would type."));
        }

        return fixes;
    }

    /// <summary><paramref name="word"/> with the first number that is not in <paramref name="used"/>.</summary>
    /// <remarks>
    /// The number starts at one, because <c>Rule</c> on its own reads like a concept rather than like a node
    /// of one. The chosen name joins the used set, so two unnamed rules are offered <c>Rule1</c> and
    /// <c>Rule2</c> rather than both being offered <c>Rule1</c>. <paramref name="rule"/> respells the
    /// candidate, so a language that says how its names are written gets a repair that satisfies it - a host
    /// check that wrote a name the language's own <c>STD004</c> would report would be repairing into a finding.
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

    /// <summary>
    /// The node and everything below it, in the order the persistence format writes them.
    /// </summary>
    /// <remarks>
    /// <see cref="Node.ChildrenInDocumentOrder"/> answers one level only, so the walk is here rather than
    /// there: a finding about a rule inside a ruleset is as useful as one about the ruleset, and a host
    /// that only looked at roots would report a document as fine while it holds unnamed rules.
    /// </remarks>
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

    /// <summary>The concept key without its language prefix, as prose: "Rules.Rule" is a "rule".</summary>
    private static string Describe(string conceptKey)
    {
        var word = Word(conceptKey);

        return word.Length == 0 ? "node" : char.ToLowerInvariant(word[0]) + word[1..];
    }

    /// <summary>The concept key's last segment, as an identifier: "Rules.Rule" is "Rule".</summary>
    private static string Word(string conceptKey)
    {
        var last = conceptKey.LastIndexOf('.');
        var word = last < 0 ? conceptKey : conceptKey[(last + 1)..];

        return word.Length == 0 ? "Node" : word;
    }
}