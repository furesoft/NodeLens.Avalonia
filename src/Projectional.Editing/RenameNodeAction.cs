using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Renames a node and refreshes the fallback text every reference to it stores.</summary>
/// <remarks>
/// <para>
/// A reference is stored as a target id, so a rename cannot break resolution: every usage keeps pointing at the same
/// node, and every cell that draws it shows the new name on the next rebuild. What goes stale is the stored fallback
/// text (<c>resolveInfo</c>) - the name the target had when the reference was written, kept only to re-link a
/// reference whose id is lost. This action rewrites it for every usage in the edited model, so a file written after a
/// rename can still be re-linked by name.
/// </para>
/// <para>
/// <strong>It is one transaction, so one undo step</strong>, and the fallback is rewritten through
/// <see cref="EditContext.SetReference(Node, string, Node)"/> - the same seam a repair uses, and itself the only
/// writer of a reference there is. No new model primitive and no new change kind: a reference that keeps its target
/// but refreshes its fallback is still a reference write, and undo already restores both halves through the
/// reference closures.
/// </para>
/// <para>
/// <strong>It is model-scoped.</strong> The node has to be in the document being edited, and usages whose owner is in
/// another model of the project are left alone. A write into a second model belongs to that model's own history, so
/// undoing the rename here could not take it back; the id keeps such a reference resolving, and only its fallback
/// stays as it was until that reference is next written. A node of another model is refused with a reason rather than
/// renamed outside the history that would have to undo it.
/// </para>
/// <para>
/// Renaming to the value the name already holds is <see cref="EditResult.Unchanged"/>: a history step that changes
/// nothing would make the history claim a change the model never had.
/// </para>
/// </remarks>
public sealed class RenameNodeAction : EditAction
{
    private readonly NodeId _node;
    private readonly string _name;

    /// <summary>Renames node <paramref name="node"/> to <paramref name="name"/>.</summary>
    /// <param name="node">The node to rename.</param>
    /// <param name="name">The new name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is empty.</exception>
    public RenameNodeAction(NodeId node, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (node.IsEmpty)
        {
            throw new ArgumentException("A rename needs a node.", nameof(node));
        }

        _node = node;
        _name = name;
    }

    /// <summary>The node that is renamed.</summary>
    public NodeId Node => _node;

    /// <summary>The name it is given.</summary>
    public string Name => _name;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var model = context.Document.Model;

        if (model.Project.FindNode(_node) is not { } node)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The document no longer holds the node '{_node}' this renames.");
        }

        if (!ReferenceEquals(node.Model, model))
        {
            return EditResult.Rejected(
                context.Selection,
                $"The node '{_node}' belongs to another model; open that document to rename it.");
        }

        // The declaration comes first: a concept without a name slot has nothing to rename, and asking a node for a
        // slot it does not declare would throw instead of answering.
        if (node.Concept.NameSlot is not { } nameSlot)
        {
            return EditResult.Rejected(
                context.Selection,
                $"'{node.Concept.Key}' declares no name slot, so it cannot be renamed.");
        }

        if (string.Equals(node.GetOrNull(nameSlot.Name), _name, StringComparison.Ordinal))
        {
            return EditResult.Unchanged(context.Selection);
        }

        // Read before the name changes: the index is the model's, and a usage is a place a reference is stored.
        var usages = ReferenceIndex.For(model.Project).UsagesOf(node);

        // Early returns dispose the transaction without completing it, which is the rollback: a rename either renames
        // the node and every fallback of the model, or it does nothing at all.
        using var transaction = model.BeginTransaction();

        if (!context.SetProperty(node, nameSlot.Name, _name))
        {
            return EditResult.Rejected(
                context.Selection,
                $"\"{_name}\" is not a valid name for '{node.Concept.Key}'.");
        }

        foreach (var usage in usages)
        {
            // A fallback in another model belongs to that model's history; see the remarks.
            if (model.Project.FindNode(usage.Owner) is not { } owner || !ReferenceEquals(owner.Model, model))
            {
                continue;
            }

            if (!context.SetReference(owner, usage.Slot, node))
            {
                return EditResult.Rejected(
                    context.Selection,
                    $"The reference '{usage.Slot}' could not be refreshed.");
            }
        }

        transaction.Complete();

        return EditResult.Applied(Selection.At(CellAddress.OfSlot(node.Id, nameSlot.Name)));
    }

    public override string ToString() => $"rename {_node} to \"{_name}\"";
}
