using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Answers which declaration a reference at the caret points at - the question behind Ctrl+B.</summary>
/// <remarks>
/// <para>
/// Only a reference cell names something this can go to; every other cell names nothing. That is deliberately
/// narrower than <see cref="FindUsages.TargetOf"/>, which answers "which node does the caret ask about" for every
/// caret: find usages can ask about the node a caret stands in, while going to a definition only means something
/// where the caret stands on a use.
/// </para>
/// <para>
/// A target has to be <em>drawn</em> in this session's tree to be a place the caret can go. A session projects one
/// root, and a reference may point at a sibling root, at another model, or at a concept whose notation the language
/// has not written - in all three cases there is no cell to land on. The menu asks
/// <see cref="CanNavigate"/> and grays the entry out instead of offering a jump that would only move the caret
/// somewhere unrelated.
/// </para>
/// </remarks>
public static class GoToDefinition
{
    /// <summary>The node the reference at the caret points at, or <see langword="null"/> when there is none.</summary>
    /// <param name="session">The session whose caret and tree are read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// An unresolved reference answers <see langword="null"/> like an unset one: it names an id, but no node a caret
    /// could be sent to. A target of another model is returned - it is the declaration the reference means - and
    /// <see cref="CanNavigate"/> is what says it cannot be shown here.
    /// </remarks>
    public static Node? TargetOf(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return TargetOf(session.Tree, session.Selection, session.Document);
    }

    /// <summary>Whether Ctrl+B would move the caret somewhere in <paramref name="session"/>.</summary>
    /// <param name="session">The session whose caret and tree are read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static bool CanNavigate(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return PlaceOf(session.Tree, session.Document, TargetOf(session)) is not null;
    }

    /// <summary>The node a reference at <paramref name="selection"/>'s caret points at.</summary>
    internal static Node? TargetOf(EditorTree tree, Selection selection, Node document)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(document);

        if (CaretNavigation.PlaceAt(tree, selection.Caret) is not { Cell: ReferenceCell reference })
        {
            return null;
        }

        return reference.Target is { } target ? document.Model.Project.FindNode(target) : null;
    }

    /// <summary>The address the caret should land on, or <see langword="null"/> when the target is not on this page.</summary>
    /// <param name="tree">The projection the caret has to land in.</param>
    /// <param name="document">The node the tree projects, which is the model a landing place has to belong to.</param>
    /// <param name="target">The declaration a reference points at, or <see langword="null"/>.</param>
    /// <remarks>
    /// A declaration is named by its name cell when the notation draws it, because the name is what a reader looks
    /// for. Otherwise the node itself is the place - and the drawn test is on the owner rather than on the node
    /// address, because a child's projection root carries its parent's element address while the cells inside it carry
    /// the child's own.
    /// </remarks>
    internal static CellAddress? PlaceOf(EditorTree tree, Node document, Node? target)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(document);

        if (target is null || !ReferenceEquals(target.Model, document.Model))
        {
            return null;
        }

        if (target.Concept.NameSlot is { } nameSlot)
        {
            var named = CellAddress.OfSlot(target.Id, nameSlot.Name);

            if (tree.Find(named) is not null)
            {
                return named;
            }
        }

        return tree.Cells().Any(cell => cell.Address.Owner == target.Id) ? new CellAddress(target.Id) : null;
    }
}
