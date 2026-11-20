using Projectional.Core;
using Projectional.Editing;

namespace Projectional.Workbench;

/// <summary>Turns a usage report into the rows the panel shows.</summary>
/// <remarks>
/// <para>
/// The order is the report's - models in project order, nodes depth first - because a list of usages is read the way
/// the documents are, not sorted by name or by how close the row is to the caret.
/// </para>
/// <para>
/// A usage whose owner is gone still gets a row: the report is a snapshot, and a row that silently disappeared would
/// claim the reference is still there. It sorts where the report put it, because the index is rebuilt on every change
/// and a missing owner means the model changed between the question and the answer rather than a row to sort last.
/// </para>
/// </remarks>
internal static class UsageList
{
    /// <summary>The rows for <paramref name="report"/>.</summary>
    /// <param name="report">The answer to one find-usages question.</param>
    internal static IReadOnlyList<UsageEntry> Of(UsageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Target is not { } target)
        {
            return [];
        }

        var project = target.Model.Project;
        List<UsageEntry> rows = [];

        foreach (var usage in report.Usages)
        {
            var owner = project.FindNode(usage.Owner);
            var model = owner?.Model ?? target.Model;
            var label = owner is null ? "a node that is gone" : LabelOf(owner);

            // A second model is invisible from the row's own text, so the row says which one when it is not the
            // target's - one project is several documents, and a caller has to know where to look.
            var prefix = ReferenceEquals(model, target.Model) ? string.Empty : $"{model.Name ?? "Untitled"}: ";

            rows.Add(new UsageEntry(model, usage.Place, $"{prefix}{label} - {usage.Slot}"));
        }

        return rows;
    }

    /// <summary>The node as a header names it: its concept and, when it has one, its name.</summary>
    /// <param name="node">The node the question was about.</param>
    /// <remarks>
    /// A concept key is the fallback rather than an id, because a header is read: "Usages of Test.Rule" is an answer,
    /// "Usages of 6f4c..." is not.
    /// </remarks>
    internal static string LabelOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Name is { Length: > 0 } name
            ? $"{node.Concept.Key} \"{name}\""
            : node.Concept.Key;
    }
}
