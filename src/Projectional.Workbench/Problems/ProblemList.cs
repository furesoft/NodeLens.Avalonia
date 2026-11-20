using Projectional.Core;
using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Workbench;

/// <summary>Everything the checks found in one model, as rows a user can walk through.</summary>
/// <remarks>
/// <para>
/// The order is the document's, not the severity's: a list sorted by severity would make "the next problem" a
/// meaningless gesture, because fixing one row would move every other row. A user reads a document from the top, so
/// the panel does too - and the worst finding in a stretch of text is already carried by the severity each row shows.
/// </para>
/// <para>
/// A finding whose node is no longer in the model still gets a row, at the end. Findings are snapshots taken when the
/// document changed, and the panel must not pretend a stale one never happened; what it must not do is offer to jump
/// to a node that is gone, which is why the row keeps its place but sorts last.
/// </para>
/// </remarks>
internal static class ProblemList
{
    /// <summary>The rows for <paramref name="findings"/>, in document order.</summary>
    /// <param name="model">The model the findings were produced from.</param>
    /// <param name="findings">What the checks found.</param>
    internal static IReadOnlyList<ProblemEntry> Of(Model model, IEnumerable<Diagnostic> findings)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(findings);

        var order = DocumentOrder(model);

        List<(int Order, string Role, int Index, ProblemEntry Entry)> rows = [];

        foreach (var finding in findings)
        {
            rows.Add((
                order.TryGetValue(finding.Target, out var index) ? index : int.MaxValue,
                finding.Role ?? string.Empty,
                finding.Index ?? -1,
                new ProblemEntry(PlaceOf(model, finding), finding.Severity, TextOf(model, finding))));
        }

        rows.Sort(static (left, right) =>
        {
            var byPosition = left.Order.CompareTo(right.Order);

            if (byPosition != 0)
            {
                return byPosition;
            }

            var byRole = string.CompareOrdinal(left.Role, right.Role);

            return byRole != 0 ? byRole : left.Index.CompareTo(right.Index);
        });

        return [.. rows.Select(row => row.Entry)];
    }

    /// <summary>Where the caret goes to look at <paramref name="finding"/>.</summary>
    /// <remarks>
    /// The slot when the finding named one, because a finding about a name is a finding about that cell rather than
    /// about the whole node - the node address would land the caret wherever the notation starts, which is a different
    /// place entirely for a node drawn over many lines.
    /// </remarks>
    private static CellAddress PlaceOf(Model model, Diagnostic finding) =>
        finding.Role is { } role
        && model.Project.FindNode(finding.Target)?.Concept is { } concept
        && (concept.FindChild(role) is not null || concept.FindProperty(role) is not null || concept.FindReference(role) is not null)
            ? CellAddress.OfSlot(finding.Target, role)
            : new CellAddress(finding.Target);

    /// <summary>The finding as one line, named by what it is about rather than by an id.</summary>
    private static string TextOf(Model model, Diagnostic finding)
    {
        var about = model.Project.FindNode(finding.Target) is { } node
            ? node.Name is { Length: > 0 } name
                ? name
                : node.Concept.Key
            : "a node that is gone";

        return $"{finding.Message} - {about}";
    }

    /// <summary>The position of every node in its model, depth first.</summary>
    private static Dictionary<NodeId, int> DocumentOrder(Model model)
    {
        Dictionary<NodeId, int> order = [];
        var index = 0;

        foreach (var node in model.Nodes)
        {
            order[node.Id] = index++;
        }

        return order;
    }
}