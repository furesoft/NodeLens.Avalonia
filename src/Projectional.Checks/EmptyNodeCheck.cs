using Projectional.Core;
using Projectional.Editing;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A list entry that holds nothing but the defaults its concept gives it.</summary>
/// <remarks>
/// <para>
/// This is the node a user created and forgot: an empty rule, a step with no text, a transition that leads
/// nowhere. Nothing in the document misses it, nothing refers to it, and it says nothing - so the one repair
/// that helps is to take it out again, which is why the catalogue gives it a destructive fix rather than a
/// warning.
/// </para>
/// <para>
/// Three exclusions decide what "empty" means, and each is a fact rather than a taste:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Only a list entry.</b> Emptying a single-child slot is a different act - the slot stays, and where it
/// is required it is reported by <see cref="EmptyChildSlotCheck"/> - so this check never offers to delete the
/// only child of one.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Only a node that has slots to fill.</b> A leaf whose whole content is one value (<c>IntLiteral</c>,
/// <c>Name</c>, <c>Sm.State</c>) <em>is</em> that value, and the model cannot tell a value the user typed from
/// one it holds by default. Reporting those would put "delete the empty 0" under every zero.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Nothing set anywhere.</b> A property holding a non-default value, one child in any slot, or one
/// reference written is content, and content is what makes the node worth keeping.
/// </description>
/// </item>
/// </list>
/// <para>
/// A language whose empty container is meaningful must therefore leave this check out: an empty JSON object
/// and an empty XML element are values a file may legitimately hold, and deleting one would change what the
/// file says. That is the catalogue's usual answer - a language includes what it wants, and nothing has to
/// be switched off.
/// </para>
/// <para>
/// The finding is a <see cref="DiagnosticSeverity.Hint"/>, and the repair is a
/// <see cref="DeleteNodeAction(NodeId)"/>: the node the finding names, not the one under the caret, because a
/// repair runs wherever the user happens to be.
/// </para>
/// </remarks>
public sealed class EmptyNodeCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD012";

    /// <summary>The stable identifier of the repair that deletes the node.</summary>
    public const string FixId = "std.empty-node.delete";

    /// <summary>Every list entry in <paramref name="model"/> that holds nothing but its defaults.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per empty node, in document order. The diagnostic names the node itself, because there is no
    /// single slot the finding is about.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var node in NodeWalk.Of(model))
        {
            // A root is the document rather than an entry a reader added to a list, so it has no place a
            // deletion could take it out of.
            if (node.Parent is not { } parent ||
                parent.Concept.FindChild(node.ParentSlot!) is not { Cardinality: Cardinality.Many } ||
                !HoldsNothing(node))
            {
                continue;
            }

            var id = node.Id;

            fixes.Add(new DiagnosticFix(
                new Diagnostic(
                    DiagnosticSeverity.Hint,
                    id,
                    $"this {ConceptWords.Describe(node.Concept.Key)} holds nothing but its defaults.",
                    code: Code),
                FixId,
                $"Delete the empty {ConceptWords.Describe(node.Concept.Key)}",
                context => context.Node is null ? null : new DeleteNodeAction(id),
                "Removes the node, which is what an entry nobody filled was meant to be.",
                safety: EditSafety.Destructive));
        }

        return fixes;
    }

    /// <summary>Whether <paramref name="node"/> has nothing but the values its concept declares for it.</summary>
    /// <remarks>
    /// A property with no declared default is empty while it is unset, and one with a default is empty while it
    /// still equals it - the question <see cref="PlaceholderNameCheck"/> asks about the name, asked of every
    /// slot. A concept that declares no child slot at all is never empty: see the class remarks.
    /// </remarks>
    private static bool HoldsNothing(Node node)
    {
        var hasChildSlot = false;

        foreach (var declaration in node.Concept.Slots)
        {
            switch (declaration)
            {
                case PropertySlot property:
                    if (!string.Equals(node.GetOrNull(property.Name), property.DefaultValue, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    break;

                case ChildSlot child:
                    hasChildSlot = true;

                    if (node.GetChildren(child.Name).Count > 0)
                    {
                        return false;
                    }

                    break;

                case ReferenceSlot reference:
                    if (node.GetReference(reference.Name) is not null)
                    {
                        return false;
                    }

                    break;
            }
        }

        return hasChildSlot;
    }
}
