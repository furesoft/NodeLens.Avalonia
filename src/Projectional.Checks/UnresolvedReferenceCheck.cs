using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Checks;

/// <summary>A reference whose target is gone, repaired by pointing it at a name the scope offers - or by clearing it.</summary>
/// <remarks>
/// <para>
/// A reference keeps its target id <em>and</em> the text it was written with, so a document that lost a target still
/// says what it meant. That text is what makes a repair possible: the language's own scope is asked what is visible
/// here, and the entries whose names are close to the text become one offer each. This is the standard check that
/// could only exist because the scope chain is already a language aspect - none of it is knowledge about a language.
/// </para>
/// <para>
/// <b>One finding, several offers.</b> The finding is the dangling reference; the repairs are the candidates.
/// <see cref="FixSet"/> keys on the finding <em>and</em> the fix id, so every offer carries the target's node id in
/// its id - three close names are three rows, not one row that has to be reworded.
/// </para>
/// <para>
/// <b>When nothing is close, the only honest offer is to clear it</b> (<c>STD021</c>), and that is destructive:
/// the name it kept is the last thing that says what was meant. Two ids rather than two checks, because one
/// reference must not produce two findings - which of the two situations holds is a fact about the same broken
/// pointer.
/// </para>
/// <para>
/// <b>Closeness is a spelling question, not a semantic one.</b> Equal, equal ignoring case, one a prefix of the
/// other, or at most two characters apart - in that order, and the scope's own order breaks ties. A repair the
/// check cannot justify is worse than no repair, so anything further away is left to the completion list the user
/// can open themselves.
/// </para>
/// </remarks>
public sealed class UnresolvedReferenceCheck : ICheck
{
    /// <summary>The identifier of a finding that has at least one close candidate.</summary>
    public const string Code = "STD020";

    string ICheck.Code => Code;

    string ICheck.Title => "A reference whose target is gone, repaired by pointing it at a name the scope offers - or by clearing it.";

    /// <summary>The identifier of a finding whose only repair is clearing the reference.</summary>
    public const string ClearCode = "STD021";

    /// <summary>The prefix of the stable identifier of a "point it there" repair.</summary>
    /// <remarks>
    /// The target's node id is appended, because one finding offers one repair per candidate and
    /// <see cref="FixSet"/> refuses two repairs that share an id for one finding.
    /// </remarks>
    public const string PointFixId = "std.unresolved-reference.point";

    /// <summary>The stable identifier of the repair that clears the reference.</summary>
    public const string ClearFixId = "std.unresolved-reference.clear";

    private readonly IScopeProvider _scopes;

    /// <summary>Reports dangling references, asking <paramref name="scopes"/> what could stand in their place.</summary>
    /// <param name="scopes">
    /// The language's scope aspect, or <see langword="null"/> for <see cref="DefaultScopeProvider.Instance"/> - the
    /// same default a session uses, so a language that replaces one level must hand its provider here as well.
    /// </param>
    public UnresolvedReferenceCheck(IScopeProvider? scopes = null) => _scopes = scopes ?? DefaultScopeProvider.Instance;

    /// <summary>Every dangling reference in <paramref name="model"/>, in document order.</summary>
    /// <param name="model">The document to inspect; every root is walked, whole subtrees included.</param>
    /// <returns>
    /// One finding per dangling reference: a repair per close candidate when there is any, and the clearing repair
    /// when there is none. A reference that resolves - in this model or in another document of the project - is not
    /// reported, and neither is one nobody has set.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<DiagnosticFix> Find(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<DiagnosticFix> fixes = [];

        foreach (var node in NodeWalk.Of(model))
        {
            foreach (var declaration in node.Concept.Slots)
            {
                if (declaration is not ReferenceSlot slot
                    || node.GetReference(slot.Name) is not { } reference
                    || reference.IsResolvedIn(model.Project))
                {
                    continue;
                }

                Collect(model, node, slot, reference, fixes);
            }
        }

        return fixes;
    }

    private void Collect(
        Model model,
        Node node,
        ReferenceSlot slot,
        NodeReference reference,
        List<DiagnosticFix> fixes)
    {
        var text = reference.ResolveInfo;
        var candidates = CandidatesOf(model, node, slot, text);
        var finding = new Diagnostic(
            DiagnosticSeverity.Warning,
            node.Id,
            MessageOf(text, candidates.Count),
            role: slot.Name,
            code: candidates.Count > 0 ? Code : ClearCode);

        if (candidates.Count == 0)
        {
            fixes.Add(ClearFixOf(finding, node, slot));

            return;
        }

        foreach (var candidate in candidates)
        {
            fixes.Add(PointFixOf(finding, node, slot, candidate));
        }
    }

    private static DiagnosticFix PointFixOf(Diagnostic finding, Node node, ReferenceSlot slot, ScopeEntry candidate)
    {
        var owner = node.Id;
        var target = candidate.Node;

        return new DiagnosticFix(
            finding,
            $"{PointFixId}.{target}",
            $"Point to \"{candidate.Name}\"",
            context => context.Node is null ? null : new SetReferenceAction(owner, slot.Name, target),
            "Points the reference at the node this slot's scope offers under that name.");
    }

    private static DiagnosticFix ClearFixOf(Diagnostic finding, Node node, ReferenceSlot slot)
    {
        var owner = node.Id;

        return new DiagnosticFix(
            finding,
            ClearFixId,
            "Clear the reference",
            context => context.Node is null ? null : new SetReferenceAction(owner, slot.Name),
            "Removes the reference; nothing visible here is close to the name it kept.",
            safety: EditSafety.Destructive);
    }

    /// <summary>The scope's entries for <paramref name="slot"/> that are close to <paramref name="text"/>, best first.</summary>
    private List<ScopeEntry> CandidatesOf(Model model, Node node, ReferenceSlot slot, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            // Nothing was kept, so there is no spelling to be close to and no offer this check could justify.
            return [];
        }

        var ranked = new List<(int Rank, int Order, ScopeEntry Entry)>();
        var entries = _scopes.ScopeOf(node, slot.Name).Entries;

        for (var order = 0; order < entries.Count; order++)
        {
            var entry = entries[order];

            if (entry.Fits(slot.TargetConceptKey, model.Concepts) && RankOf(text, entry.Name) is { } rank)
            {
                ranked.Add((rank, order, entry));
            }
        }

        // The scope's order is the tie-break, because that is the order the language already ranks its names in.
        ranked.Sort((left, right) => left.Rank != right.Rank
            ? left.Rank.CompareTo(right.Rank)
            : left.Order.CompareTo(right.Order));

        return [.. ranked.Select(entry => entry.Entry)];
    }

    /// <summary>How close <paramref name="candidate"/> is to <paramref name="text"/>, or <see langword="null"/> when it is not close.</summary>
    /// <remarks>
    /// Four rungs, and the number is only used for ordering: the same name, the same name in another case, one that
    /// continues the other, and a name a keystroke or two away. The last one is bounded so a long unrelated word
    /// cannot become a repair nobody would accept.
    /// </remarks>
    private static int? RankOf(string text, string candidate)
    {
        if (string.Equals(text, candidate, StringComparison.Ordinal))
        {
            return 0;
        }

        if (string.Equals(text, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (candidate.StartsWith(text, StringComparison.OrdinalIgnoreCase)
            || text.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return Distance(text, candidate) <= 2 ? 3 : null;
    }

    /// <summary>How many single-character edits turn <paramref name="left"/> into <paramref name="right"/>.</summary>
    /// <remarks>
    /// Case-insensitive and bounded by the caller: the length difference alone settles most pairs, which is what
    /// makes a two-row table enough here instead of a general-purpose algorithm.
    /// </remarks>
    private static int Distance(string left, string right)
    {
        if (Math.Abs(left.Length - right.Length) > 2)
        {
            return int.MaxValue;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var same = char.ToLowerInvariant(left[row - 1]) == char.ToLowerInvariant(right[column - 1]);

                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + (same ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static string MessageOf(string? text, int candidates) => text switch
    {
        null or "" => "the reference does not resolve.",
        _ when candidates > 0 => $"the reference to \"{text}\" does not resolve.",
        _ => $"the reference to \"{text}\" does not resolve, and nothing visible here is close.",
    };
}