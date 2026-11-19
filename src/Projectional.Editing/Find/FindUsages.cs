using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Answers who points at the node the caret names, or at a node named outright.</summary>
/// <remarks>
/// <para>
/// The question is the model's, so the search is <see cref="ReferenceIndex"/>: it walks every model of the project
/// once and answers in document order, which is what a rename needs before it may rewrite anything and what a usages
/// list shows. This layer contributes only the two things a projection knows - which node the caret is asking about,
/// and where each usage is drawn.
/// </para>
/// <para>
/// <strong>A caret on a reference asks about its target; a caret anywhere else asks about the node it is in.</strong>
/// That is the same reading as MPS' gesture: standing on a name asks "who points here", and standing on a use of a
/// name asks about the declaration it resolves to. A caret on an unresolved reference asks about nothing - there is no
/// node to answer for - which is a different answer from a node nothing references.
/// </para>
/// <para>
/// A usage the notation hides is still reported. The index is the model's, and a hidden reference is exactly the case
/// a rename must not miss; only the jump is a page question, which is why a caller asks
/// <see cref="EditorTree.Find(CellAddress)"/> before it selects.
/// </para>
/// </remarks>
public static class FindUsages
{
    /// <summary>Who points at the node the caret names in <paramref name="session"/>.</summary>
    /// <param name="session">The session whose caret asks the question.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static UsageReport ReportOf(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return TargetOf(session) is { } target ? Of(target) : UsageReport.None;
    }

    /// <summary>The node the caret in <paramref name="session"/> asks about, or <see langword="null"/> when it names none.</summary>
    /// <param name="session">The session whose caret is read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The cell decides: a reference cell names its target, everything else names the node whose address it carries.
    /// An empty child slot carries its parent's address, which is the answer the intention list gives too - "who
    /// points at this" about an empty slot is about the node that owns it.
    /// </remarks>
    public static Node? TargetOf(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret) is not { } place)
        {
            return null;
        }

        var project = session.Document.Model.Project;

        if (place.Cell is ReferenceCell reference)
        {
            return reference.Target is { } target ? project.FindNode(target) : null;
        }

        return project.FindNode(place.Address.Owner);
    }

    /// <summary>Who points at <paramref name="target"/>.</summary>
    /// <param name="target">The node whose usages are wanted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The same answer <see cref="ReportOf"/> gives, for a caller that already knows the node - a panel that stays open
    /// across an edit re-asks about the node it was opened for, rather than about wherever the caret has moved to.
    /// </remarks>
    public static UsageReport Of(Node target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var usages = ReferenceIndex.For(target.Model.Project).UsagesOf(target);

        return new UsageReport(
            target,
            [.. usages.Select(usage => new UsageMatch(usage.Owner, usage.Slot, usage.Target))]);
    }
}
