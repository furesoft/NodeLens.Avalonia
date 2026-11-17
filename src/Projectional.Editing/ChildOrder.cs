using Projectional.Core;

namespace Projectional.Editing;

/// <summary>The order the children of a list slot would have if they were sorted by name.</summary>
/// <remarks>
/// <para>
/// The rule lives here rather than in the model because it is a <em>decision</em>: the model reorders and owns what
/// must not vary, while "sorted by what" is a choice the caller makes. It lives in <c>Projectional.Editing</c> rather
/// than in one action because two callers ask the same question - the action that performs the sort, and the entry
/// that offers it only when it would move something - and a second spelling of "by name" would be the one that
/// drifts.
/// </para>
/// <para>
/// Three refusals, all answered as <see langword="null"/> rather than as an exception, because "this list cannot be
/// sorted" is an ordinary answer:
/// </para>
/// <list type="bullet">
/// <item>a single-valued slot has no order to change;</item>
/// <item>fewer than two children is nothing to rearrange;</item>
/// <item>a child whose concept declares no name slot has no value to sort by - and sorting such a list by the ones
/// that do have a name would put the rest in an order nobody asked for, so the whole answer is "no".</item>
/// </list>
/// <para>
/// The comparison is ordinal, case-sensitive, and stable: two children with one name keep the order they already had,
/// which is what makes the operation safe to run twice.
/// </para>
/// </remarks>
public static class ChildOrder
{
    /// <summary>The children of <paramref name="slot"/>, sorted by their name; or <see langword="null"/>.</summary>
    /// <param name="parent">The node that owns the list.</param>
    /// <param name="slot">A child slot declared with <see cref="Cardinality.Many"/>.</param>
    public static IReadOnlyList<Node>? ByName(Node parent, string slot)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (parent.Concept.FindChild(slot) is not { Cardinality: Cardinality.Many })
        {
            return null;
        }

        var children = parent.GetChildren(slot);

        if (children.Count < 2)
        {
            return null;
        }

        foreach (var child in children)
        {
            if (child.Concept.NameSlot is null)
            {
                return null;
            }
        }

        return
        [
            .. children.OrderBy(
                child => child.GetOrNull(child.Concept.NameSlot!.Name) ?? string.Empty,
                StringComparer.Ordinal),
        ];
    }

    /// <summary>Whether sorting <paramref name="slot"/> by name would move anything.</summary>
    /// <param name="parent">The node that owns the list.</param>
    /// <param name="slot">A child slot declared with <see cref="Cardinality.Many"/>.</param>
    /// <remarks>
    /// The catalogue's own rule is that an entry which would change nothing is not offered, and a list that is already
    /// in name order is exactly that case - without this question the entry would light the bulb and produce no edit.
    /// </remarks>
    public static bool WouldChange(Node parent, string slot)
    {
        if (ByName(parent, slot) is not { } order)
        {
            return false;
        }

        var children = parent.GetChildren(slot);

        for (var index = 0; index < children.Count; index++)
        {
            if (!ReferenceEquals(children[index], order[index]))
            {
                return true;
            }
        }

        return false;
    }
}
