using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.Workbench;

/// <summary>Finds the nodes that are still called the name their concept starts them with.</summary>
/// <remarks>
/// <para>
/// This is the app's own check rather than the language's, and that is the point of it: a host publishes
/// findings by handing the control a <c>MarkerSet</c>, so the shell has to be able to produce one without
/// the model knowing anything about checks. The rules language declares no semantics yet, so a real
/// analysis would have to invent them; "this name is still the placeholder" needs nothing but the model.
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
/// </remarks>
internal static class DefaultNames
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    internal const string Code = "SHELL001";

    /// <summary>What is still unnamed in <paramref name="model"/>.</summary>
    /// <param name="model">The model to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One finding per unnamed node, in document order, each targeting the name property that carries the
    /// placeholder so the editor can draw it under the word rather than under the whole node.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyList<Diagnostic> Of(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<Diagnostic> findings = [];

        foreach (var root in model.Roots)
        {
            foreach (var node in SelfAndDescendants(root))
            {
                // A concept without a name slot is not "unnamed", it is anonymous by design - a literal,
                // an operator. Only a declared placeholder is something the user was meant to replace.
                if (node.Concept.NameSlot is { DefaultValue: { Length: > 0 } placeholder } nameSlot
                    && node.GetOrNull(nameSlot.Name) == placeholder)
                {
                    findings.Add(new Diagnostic(
                        DiagnosticSeverity.Hint,
                        node.Id,
                        $"this {Describe(node.Concept.Key)} is still called \"{placeholder}\"; give it a name.",
                        role: nameSlot.Name,
                        code: Code));
                }
            }
        }

        return findings;
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
        var last = conceptKey.LastIndexOf('.');
        var name = last < 0 ? conceptKey : conceptKey[(last + 1)..];

        return name.Length == 0 ? "node" : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
