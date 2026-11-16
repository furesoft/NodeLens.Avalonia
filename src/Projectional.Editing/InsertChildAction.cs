using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Fills the empty child slot of a named node with the concept the language offers there.
/// </summary>
/// <remarks>
/// <para>
/// This is <see cref="InsertNodeAction"/>'s first behaviour with the caret taken out of the question: the
/// caller names the owner and the slot instead of standing in one. That is what a repair needs, because a
/// check runs on a model nobody is drawing and must be able to say <em>which</em> hole it fills - moving the
/// caret there first would be a side effect the user never asked for.
/// </para>
/// <para>
/// The two actions share this one, so the repair and the <c>Enter</c> key cannot drift apart: the concept
/// comes from <see cref="ConceptRegistry.DefaultInstantiableFor"/>, which is the language's own answer to
/// "what does the user most likely want here".
/// </para>
/// <para>
/// The concept is chosen at apply time rather than when the action was built, so an action that sat in a
/// queue while the language was replaced still creates something the current registry declares.
/// </para>
/// </remarks>
public sealed class InsertChildAction : EditAction
{
    private readonly NodeId _owner;
    private readonly string _slot;

    /// <summary>Fills <paramref name="slot"/> of <paramref name="owner"/>.</summary>
    /// <param name="owner">The node that owns the empty slot.</param>
    /// <param name="slot">The child slot to fill, which must be empty when this runs.</param>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is empty or <paramref name="slot"/> is not.</exception>
    public InsertChildAction(NodeId owner, string slot)
    {
        if (owner.IsEmpty)
        {
            throw new ArgumentException("An insertion needs an owner node.", nameof(owner));
        }

        ArgumentException.ThrowIfNullOrEmpty(slot);

        _owner = owner;
        _slot = slot;
    }

    /// <summary>The node whose slot is filled.</summary>
    public NodeId Owner => _owner;

    /// <summary>The child slot that is filled.</summary>
    public string Slot => _slot;

    /// <inheritdoc/>
    /// <remarks>
    /// A node materializes its concept's defaults as soon as it exists, so what was created is always
    /// something to name - inherited from <see cref="InsertNodeAction"/> because this is one of its two
    /// behaviours, not a different idea.
    /// </remarks>
    public override bool SelectsLandedValue => true;

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.Document.Model.Project.FindNode(_owner) is not { } owner)
        {
            return EditResult.Rejected(
                selection,
                "The node this slot belongs to is no longer in the document.");
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

        // A slot declared over an abstract concept whose every descendant is abstract is a set with nothing
        // in it. Enter is silent there rather than complaining, because the keystroke asked for "another
        // thing here" and the language has no thing to offer - so this is no answer, not a refusal.
        if (context.Concepts.DefaultInstantiableFor(declared.ChildConceptKey, owner.Concept, _slot) is not { } concept)
        {
            return EditResult.Unchanged(selection);
        }

        var created = context.AddChild(owner, _slot, concept.Key);

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(owner.Id, _slot, created.IndexInParent)));
    }

    public override string ToString() => $"insert into {_slot}";
}
