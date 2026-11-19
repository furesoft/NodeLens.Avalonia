using System.Globalization;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>Two children of one slot that answer to the same name.</summary>
/// <remarks>
/// <para>
/// A name is how a reference finds its target, so two siblings of one slot under one name are one word with
/// two meanings: the projection draws two identical cells, a scope lookup answers "ambiguous", and a generated
/// file carries the same key twice. This check is the declaration-side half of that - the ambiguity constraint
/// speaks at the reference that no longer knows what it points at, this one speaks at the node nobody can name
/// uniquely any more.
/// </para>
/// <para>
/// The group is one slot of one node, because that is the only place siblings exist. A duplicate across two
/// lists is ordinary - two tables may each have a <c>name</c> column - and stays quiet. Roots are not checked
/// either: they stand in no slot, and a name two of them share is reported where it hurts, at the reference.
/// </para>
/// <para>
/// The <em>later</em> child is reported, in document order, because the first one a reader meets keeps the name
/// it has: which of two identical names was meant is not knowable, and renaming the earlier one would rewrite
/// what the reader has already understood. For the same reason the proposals are computed as a group - three
/// rules called <c>Check</c> are offered <c>Check2</c> and <c>Check3</c>, not <c>Check2</c> twice, which would be
/// a repair that only works if the user applies the first finding.
/// </para>
/// <para>
/// A child whose name still equals its concept's declared placeholder is not reported: that is
/// <see cref="PlaceholderNameCheck"/>'s finding, and two nodes called <c>unnamed</c> are not two nodes that were
/// given one name.
/// </para>
/// <para>
/// The check stays silent about a node that anything in the project points at. Renaming it would leave those
/// references dangling, and a standard repair may not break a document it never saw; updating the referrers is
/// the rename refactoring's job, which is allowed to know how to find them. The scan is over
/// <see cref="Project.Nodes"/> rather than over this model, because a reference may cross a model boundary -
/// two documents of one project share the node index and the scope provider already offers each other's names.
/// </para>
/// <para>
/// The sample languages each ship this rule written by hand (<c>RULES001</c>, <c>FM001</c>, <c>SQL001</c>) with
/// their own sentence and severity. A language takes one or the other and never both, exactly as the workbench
/// keeps its own <c>SHELL001</c> beside <see cref="PlaceholderNameCheck"/>.
/// </para>
/// </remarks>
public sealed class DuplicateNameCheck : ICheck
{
    /// <summary>The identifier a host puts on a finding so a user can look it up.</summary>
    public const string Code = "STD002";

    /// <summary>The stable identifier of the repair that renames the later child.</summary>
    public const string FixId = "std.duplicate-name.rename";

    /// <summary>Every child in <paramref name="model"/> whose name an earlier sibling of the same slot has.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One fix per duplicate, in document order and, within a node, in child order. The diagnostic targets the
    /// duplicate's name property, so the marker lands under the word the user has to change.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];
        HashSet<NodeId>? referenced = null;

        foreach (var node in NodeWalk.Of(model))
        {
            foreach (var declaration in node.Concept.Slots)
            {
                if (declaration is not ChildSlot { Cardinality: Cardinality.Many } slot)
                {
                    continue;
                }

                var children = node.GetChildren(slot.Name);

                // One child cannot repeat a name, and asking for a slot's children costs a dictionary lookup
                // per node, so the common case leaves before the set is even created.
                if (children.Count < 2)
                {
                    continue;
                }

                HashSet<string> taken = new(StringComparer.Ordinal);

                foreach (var child in children)
                {
                    if (child.Name is not { Length: > 0 } name || IsPlaceholder(child, name))
                    {
                        continue;
                    }

                    if (taken.Add(name))
                    {
                        continue;
                    }

                    // Built once, and only for a document that has a duplicate at all: a check runs after
                    // every change, so the common case must not pay for a project-wide walk.
                    referenced ??= Referenced(model.Project);

                    if (referenced.Contains(child.Id))
                    {
                        continue;
                    }

                    fixes.Add(FixFor(node, child, name, FirstFree(name, taken)));
                }
            }
        }

        return fixes;
    }

    /// <summary>The fix that renames <paramref name="child"/> to <paramref name="proposal"/>.</summary>
    /// <param name="parent">The node whose slot holds both names.</param>
    /// <param name="child">The later of the two children, which is the one renamed.</param>
    /// <param name="name">The name they share, as the finding quotes it.</param>
    /// <param name="proposal">The first name beside them that nothing uses.</param>
    private static DiagnosticFix FixFor(Node parent, Node child, string name, string proposal)
    {
        var slot = child.Concept.NameSlot!.Name;
        var id = child.Id;

        return new DiagnosticFix(
            new Diagnostic(
                DiagnosticSeverity.Warning,
                id,
                $"another {ConceptWords.Describe(child.Concept.Key)} in this {ConceptWords.Describe(parent.Concept.Key)} is already called \"{name}\".",
                role: slot,
                code: Code),
            FixId,
            $"Rename to \"{proposal}\"",
            context => context.Node is null ? null : new SetPropertyAction(CellAddress.OfSlot(id, slot), proposal),
            "Renames the later node, because the first one a reader meets keeps the name it was given.");
    }

    /// <summary>Whether <paramref name="name"/> is the placeholder <paramref name="node"/>'s concept starts it with.</summary>
    private static bool IsPlaceholder(Node node, string name) =>
        node.Concept.NameSlot is { DefaultValue: { Length: > 0 } placeholder }
        && string.Equals(name, placeholder, StringComparison.Ordinal);

    /// <summary>Every node of <paramref name="project"/> that something points at.</summary>
    /// <remarks>
    /// A reference stores its target id, so this is one pass over the project's nodes and their reference slots.
    /// An unresolved reference contributes its target too: a dangling reference still names a node that was
    /// meant, and a rename would not repair it.
    /// </remarks>
    private static HashSet<NodeId> Referenced(Project project)
    {
        HashSet<NodeId> targets = [];

        foreach (var node in project.Nodes)
        {
            foreach (var declaration in node.Concept.Slots)
            {
                if (declaration is ReferenceSlot slot && node.GetReference(slot.Name) is { } reference)
                {
                    targets.Add(reference.Target);
                }
            }
        }

        return targets;
    }

    /// <summary>The first <c>name2</c>, <c>name3</c>, ... that nothing in the group uses.</summary>
    /// <remarks>
    /// Numbering starts at two, because the name itself is taken by the sibling that keeps it and
    /// <c>Check1</c> beside <c>Check</c> reads like a typo rather than like a second node. The chosen name
    /// joins the set, which is what makes the second duplicate of one name move on.
    /// </remarks>
    private static string FirstFree(string name, HashSet<string> taken)
    {
        for (var suffix = 2; ; suffix++)
        {
            var candidate = string.Concat(name, suffix.ToString(CultureInfo.InvariantCulture));

            if (taken.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
