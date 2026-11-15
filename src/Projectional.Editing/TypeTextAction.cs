using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Types text into the property the caret is in, or creates the node the language says the text belongs
/// to when the caret is in an empty slot.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of text entry: there is no separate "insert", "overwrite" or "replace" action,
/// because in a projectional editor the text of a property is the property's value, and typing is
/// therefore always a replacement of a range - of the selection if there is one, of the empty range at
/// the caret if there is not. The candidate value is built as a string and handed to the model, which
/// validates it, so a character that is not part of the property's type is refused rather than stored
/// and repaired later.
/// </para>
/// <para>
/// Inside a property cell that is the whole story: every other caret stop - a keyword, a space, a node
/// with no notation - leaves the model alone, because there is no such thing as editing the text of a
/// keyword. The notation owns it, and the user should not see that as an error, so it is reported as
/// unchanged rather than rejected.
/// </para>
/// <para>
/// An empty slot is the exception. Without one the first keystroke after inserting a concept that shows
/// empty children - a binary expression, say - would be swallowed, because the caret lands on the empty
/// child and the only property in reach belongs to a node that does not exist yet. So an empty slot asks
/// the language which concept the text belongs to, creates it, writes the text into the property the
/// rule names, and leaves the caret behind that text. The language answers rather than this action
/// because a name and a string literal can both hold <c>abc</c>, and only the language knows which one
/// the user meant.
/// </para>
/// </remarks>
public sealed class TypeTextAction : EditAction
{
    /// <summary>Creates the action for one burst of typed text.</summary>
    /// <param name="text">The characters to type; may be empty, which is a no-op.</param>
    public TypeTextAction(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
    }

    /// <summary>The characters to type.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (Text.Length == 0 || context.PlaceAt(selection.Caret) is not { } place)
        {
            return EditResult.Unchanged(selection);
        }

        if (place.Cell is PropertyCell cell)
        {
            return TypeIntoProperty(context, cell, selection);
        }

        return selection.IsEmpty && place.IsEmptySlot
            ? Create(context, place, selection)
            : EditResult.Unchanged(selection);
    }

    /// <summary>
    /// One key per place, so that a burst of typing in one property collapses into one undo step.
    /// </summary>
    /// <remarks>
    /// The place rather than the value: two keystrokes that both change the same property belong
    /// together, while a keystroke in another property is a different edit that deserves its own press
    /// of undo. The first character typed into an empty slot creates a node and fills it as one step,
    /// and the characters after it coalesce into a second.
    /// </remarks>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return KeyOf(context.Selection.Caret.Address);
    }

    public override string ToString() => $"type \"{Text}\"";

    private EditResult TypeIntoProperty(EditContext context, PropertyCell cell, Selection selection)
    {
        var caret = selection.Caret;

        var (start, end) = selection.HasText
            ? (selection.Start, selection.End)
            : (Math.Clamp(caret.Offset, 0, cell.Text.Length), Math.Clamp(caret.Offset, 0, cell.Text.Length));

        var value = string.Concat(cell.Text.AsSpan(0, start), Text.AsSpan(), cell.Text.AsSpan(end));

        if (!context.SetProperty(cell, value))
        {
            return EditResult.Rejected(selection, $"'{value}' is not a valid {cell.Type.Name}.");
        }

        return EditResult.Applied(Selection.At(CaretPosition.At(caret.Address, start + Text.Length)));
    }

    /// <summary>Creates the node the language's typing rules say the text belongs to.</summary>
    /// <remarks>
    /// The caret goes behind the typed text when the rule named a property, so that a second character
    /// appends to it rather than pushing in front of it, and inside the new node when it did not - typing
    /// <c>(</c> must leave the caret between the parentheses, not after them.
    /// </remarks>
    private EditResult Create(EditContext context, CaretPlace place, Selection selection)
    {
        var slot = place.Cell switch
        {
            ChildCell child => child.Slot,
            CollectionCell collection => collection.Slot,
            _ => null,
        };

        if (slot is null
            || context.NodeOf(place.Address) is not { } owner
            || owner.Concept.FindChild(slot) is not { } declared
            || FindClaimant(context, declared.ChildConceptKey) is not { } rule)
        {
            return EditResult.Unchanged(selection);
        }

        var created = context.AddChild(owner, slot, rule.ConceptKey, rule.PropertySlot, Text);

        if (rule.PropertySlot is not null && created.Concept.FindProperty(rule.PropertySlot) is { } property)
        {
            return EditResult.Applied(
                Selection.At(CaretPosition.At(CellAddress.OfSlot(created.Id, property.Name), Text.Length)));
        }

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(owner.Id, slot, created.IndexInParent)));
    }

    /// <summary>
    /// The first concept the slot may hold, in the order the language declared them, whose typing rule
    /// claims the text.
    /// </summary>
    /// <remarks>
    /// Declaration order rather than a vote, so that a language answers "which of these did the user
    /// mean" the same way <see cref="ConceptRegistry.DefaultInstantiableFor"/> answers it for an empty
    /// slot.
    /// </remarks>
    private TypingRule? FindClaimant(EditContext context, string declaredKey)
    {
        foreach (var concept in context.Concepts.Concepts)
        {
            if (!concept.IsAbstract
                && concept.IsKindOf(declaredKey)
                && context.TypingRules.ClaimFor(concept.Key, Text) is { } rule)
            {
                return rule;
            }
        }

        return null;
    }
}
