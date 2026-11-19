using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Inserts a node of a <em>named</em> concept into a list slot.</summary>
/// <remarks>
/// <para>
/// <see cref="InsertChildAction"/> fills an empty slot with whatever the slot's declaration most prefers, which is what
/// Enter does, and <see cref="InsertSiblingAction"/> adds a node of the <em>same</em> concept as the one it stands
/// beside - which is what "one more of these" means. Neither can say "append a table after these pairs": a named concept
/// at a chosen position. That is what a language's intentions need when a list holds several kinds of entry, as every
/// reference format's does - a TOML document holds pairs, headers and notes, and "add a table" must not become "copy
/// whatever stands last".
/// </para>
/// <para>
/// <see cref="AddChildAction"/> is the sibling for an <em>empty</em> slot and refuses a filled one on purpose. This one
/// is the list form: it asks the slot's declaration whether the concept conforms before it creates anything, so a
/// language whose menu offers something the slot refuses answers a rejection with a reason instead of throwing out of
/// the model.
/// </para>
/// <para>
/// The index is a position in the slot's children, so <c>Count</c> appends. The caret lands on the created element, which
/// is the same courtesy <see cref="InsertChildAction"/> pays: the node the user just asked for is the node they want to
/// name.
/// </para>
/// </remarks>
public sealed class InsertNamedNodeAction : EditAction
{
    private readonly NodeId _owner;
    private readonly string _slot;
    private readonly int _index;
    private readonly string _conceptKey;

    /// <summary>Inserts a node of <paramref name="conceptKey"/> into <paramref name="slot"/> at <paramref name="index"/>.</summary>
    /// <param name="owner">The node that owns the slot.</param>
    /// <param name="slot">The child slot to insert into.</param>
    /// <param name="index">The position among the slot's children; the child count appends.</param>
    /// <param name="conceptKey">The concept to create, which must conform to the slot.</param>
    /// <exception cref="ArgumentException">An identifier is empty, or <paramref name="index"/> is negative.</exception>
    public InsertNamedNodeAction(NodeId owner, string slot, int index, string conceptKey)
    {
        if (owner.IsEmpty)
        {
            throw new ArgumentException("An insertion needs an owner node.", nameof(owner));
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        _owner = owner;
        _slot = slot;
        _index = index;
        _conceptKey = conceptKey;
    }

    /// <summary>The node whose slot receives the new child.</summary>
    public NodeId Owner => _owner;

    /// <summary>The child slot the node is inserted into.</summary>
    public string Slot => _slot;

    /// <summary>The position among the slot's children.</summary>
    public int Index => _index;

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

        if (!context.Concepts.TryGet(_conceptKey, out var concept) || concept.IsAbstract)
        {
            return EditResult.Rejected(selection, $"'{_conceptKey}' is not a concept this document can create.");
        }

        if (!concept.IsKindOf(declared.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"'{_conceptKey}' cannot stand in slot '{_slot}', which holds '{declared.ChildConceptKey}'.");
        }

        if (declared.Cardinality == Cardinality.One && owner.GetChildren(_slot).Count > 0)
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{_slot}' already holds a child, so there is nothing to insert.");
        }

        var created = context.AddChild(owner, _slot, Math.Min(_index, owner.GetChildren(_slot).Count), concept.Key);

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(owner.Id, _slot, created.IndexInParent)));
    }

    public override string ToString() => $"insert {_conceptKey} into {_slot}";
}
