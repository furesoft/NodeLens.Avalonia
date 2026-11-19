using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Fills an empty child slot with a node of a <em>named</em> concept.</summary>
/// <remarks>
/// <para>
/// <see cref="InsertChildAction"/> is the gesture "put something in this slot" and takes the slot's default concept,
/// which is what Enter does. This one is the same edit with the concept decided by the caller, which is what a
/// declared intention needs - a rule that says "add a step here" must be able to name a step rather than accept
/// whatever the slot happens to prefer.
/// </para>
/// <para>
/// The slot must be empty when this runs. An insertion into a filled single-valued slot is a replacement, which is a
/// different entry, and into a list it is <see cref="InsertSiblingAction"/>'s business.
/// </para>
/// </remarks>
public sealed class AddChildAction : EditAction
{
    private readonly NodeId _owner;
    private readonly string _slot;
    private readonly string _conceptKey;

    /// <summary>Fills <paramref name="slot"/> of <paramref name="owner"/> with a node of <paramref name="conceptKey"/>.</summary>
    /// <param name="owner">The node that owns the empty slot.</param>
    /// <param name="slot">The child slot to fill, which must be empty when this runs.</param>
    /// <param name="conceptKey">The concept to create, which must conform to the slot.</param>
    /// <exception cref="ArgumentException">An identifier is empty.</exception>
    public AddChildAction(NodeId owner, string slot, string conceptKey)
    {
        if (owner.IsEmpty)
        {
            throw new ArgumentException("An insertion needs an owner node.", nameof(owner));
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        _owner = owner;
        _slot = slot;
        _conceptKey = conceptKey;
    }

    /// <summary>The node whose slot is filled.</summary>
    public NodeId Owner => _owner;

    /// <summary>The child slot that is filled.</summary>
    public string Slot => _slot;

    /// <summary>The concept that is created.</summary>
    public string ConceptKey => _conceptKey;

    /// <inheritdoc />
    public override bool SelectsLandedValue => true;

    /// <inheritdoc />
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.Document.Model.Project.FindNode(_owner) is not { } owner)
        {
            return EditResult.Rejected(selection, "The node this slot belongs to is no longer in the document.");
        }

        if (owner.Concept.FindChild(_slot) is not { } declared)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{owner.Concept.Key}' declares no child slot named '{_slot}'.");
        }

        if (owner.GetChildren(_slot).Count > 0)
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{_slot}' already holds a child, so there is nothing to insert.");
        }

        if (!context.Concepts.TryGet(_conceptKey, out var concept) || !concept.IsKindOf(declared.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"'{_conceptKey}' cannot stand in slot '{_slot}', which accepts '{declared.ChildConceptKey}'.");
        }

        var created = context.AddChild(owner, _slot, concept.Key);

        return EditResult.Applied(Selection.At(CellAddress.OfElement(owner.Id, _slot, created.IndexInParent)));
    }

    public override string ToString() => $"insert {_conceptKey} into {_slot}";
}
