using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Moves the caret to the declaration the reference at the caret points at - Ctrl+B, Ctrl+click.</summary>
/// <remarks>
/// <para>
/// A navigation, not an edit: nothing about the model changes, so the answer is <see cref="EditResult.Unchanged"/>
/// carrying the selection the caret should land on, and no undo step is created. The declaration's name is selected
/// when the notation draws it, which is what makes the target visible at a glance and is the same range the rename
/// intention lands on.
/// </para>
/// <para>
/// Two outcomes are deliberately quiet: a caret that is not on a reference, and a reference that is unset or does not
/// resolve. Both mean "there is no definition to go to", and neither is worth an error - the user may simply have
/// pressed the key somewhere else. A target that <em>exists</em> but is not drawn in this document is rejected with a
/// reason instead: the reference means something, and silence there would look like a broken gesture.
/// </para>
/// </remarks>
public sealed class GoToDefinitionAction : EditAction
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (GoToDefinition.TargetOf(context.Tree, context.Selection, context.Document) is not { } target)
        {
            return EditResult.Unchanged(context.Selection);
        }

        if (GoToDefinition.PlaceOf(context.Tree, context.Document, target) is not { } place)
        {
            return EditResult.Rejected(
                context.Selection,
                $"The definition of '{(target.Name is { Length: > 0 } name ? name : target.Concept.Key)}' is not drawn in this document.");
        }

        var named = target.Concept.NameSlot is { } nameSlot
            && place.Equals(CellAddress.OfSlot(target.Id, nameSlot.Name));

        return named
            ? new SelectSlotAction(place).Apply(context)
            : EditResult.Unchanged(Selection.At(place));
    }

    public override string ToString() => "go to definition";
}
