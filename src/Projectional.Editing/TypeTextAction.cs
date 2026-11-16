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
/// <para>
/// A property cell at the edge of a node is where triggers come in. Typing <c>+</c> after the last digit of
/// <c>5</c> is refused as a number, and a language that declares a trigger for it means "an addition, with
/// the five on its left". So a refused keystroke asks the language a second question - which is why the
/// trigger is tried only <em>after</em> the property has said no: a character a property accepts is always
/// typed, and typing <c>-</c> in front of <c>5</c> therefore still writes <c>-5</c>.
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
            var typed = TypeIntoProperty(context, cell, selection);

            return typed.Outcome == EditOutcome.Rejected
                ? Refused(context, place, selection, typed)
                : typed;
        }

        // An empty slot is the typing rules' question: a trigger grows from a node that already exists, and
        // an empty slot has none of its own to grow from.
        if (selection.IsEmpty && place.IsEmptySlot)
        {
            return Create(context, place, selection);
        }

        return Refused(context, place, selection, EditResult.Unchanged(selection));
    }

    /// <summary>
    /// What a keystroke that would otherwise do nothing does: a gesture, or the first letter of one.
    /// </summary>
    /// <param name="context">The apply in progress.</param>
    /// <param name="place">Where the caret stands.</param>
    /// <param name="selection">The selection it started with.</param>
    /// <param name="nothing">The answer to give when no gesture claims the text.</param>
    /// <remarks>
    /// When a trigger fires the sequence ends with it. When none does the characters are kept, so that the next
    /// keystroke is asked about together with them - which is the only way a word such as <c>and</c> can mean
    /// anything, since the first letter of it alone means nothing. The model is untouched either way, so the
    /// accumulation costs no undo step and disappears with the next caret move.
    /// </remarks>
    private EditResult Refused(EditContext context, CaretPlace place, Selection selection, EditResult nothing)
    {
        if (Trigger(context, place, selection) is { } applied)
        {
            context.PendingText = null;

            return applied;
        }

        context.PendingText = string.Concat(context.PendingText ?? string.Empty, Text);

        return nothing;
    }

    /// <summary>
    /// One key per place, so that a burst of typing in one property collapses into one undo step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The place rather than the value: two keystrokes that both change the same property belong
    /// together, while a keystroke in another property is a different edit that deserves its own press
    /// of undo. The first character typed into an empty slot creates a node and fills it as one step,
    /// and the characters after it coalesce into a second.
    /// </para>
    /// <para>
    /// A keystroke that becomes a trigger gets no key at all, because it restructures the tree rather than
    /// extending a value: typing <c>5</c> and then <c>+</c> must be undone in two presses, or one press would
    /// take the five away with the operator it produced.
    /// </para>
    /// </remarks>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return WouldRestructure(context) ? null : KeyOf(context.Selection.Caret.Address);
    }

    public override string ToString() => $"type \"{Text}\"";

    private EditResult TypeIntoProperty(EditContext context, PropertyCell cell, Selection selection)
    {
        var (value, start) = Compose(selection, cell);

        if (!context.SetProperty(cell, value))
        {
            return EditResult.Rejected(selection, $"'{value}' is not a valid {cell.Type.Name}.");
        }

        return EditResult.Applied(Selection.At(CaretPosition.At(selection.Caret.Address, start + Text.Length)));
    }

    /// <summary>The value this keystroke would write into <paramref name="cell"/>, and where its text starts.</summary>
    private (string Value, int Start) Compose(Selection selection, PropertyCell cell)
    {
        var caret = selection.Caret;

        var (start, end) = selection.HasText
            ? (selection.Start, selection.End)
            : (Math.Clamp(caret.Offset, 0, cell.Text.Length), Math.Clamp(caret.Offset, 0, cell.Text.Length));

        return (string.Concat(cell.Text.AsSpan(0, start), Text.AsSpan(), cell.Text.AsSpan(end)), start);
    }

    /// <summary>Whether this keystroke would put a wrapper around the node the caret is at the edge of.</summary>
    /// <remarks>
    /// The same question <see cref="Apply"/> asks, used one step earlier to decide whether the coalescing key
    /// survives. It repeats the property check rather than remembering its outcome because a key is read before
    /// the action runs - the price of keeping the decision in one place instead of threading a plan through the
    /// session.
    /// </remarks>
    private bool WouldRestructure(EditContext context)
    {
        if (!context.Selection.IsEmpty || context.PlaceAt(context.Selection.Caret) is not { } place)
        {
            return false;
        }

        if (place.Cell is PropertyCell cell && cell.Type.IsValid(Compose(context.Selection, cell).Value))
        {
            return false;
        }

        return !place.IsEmptySlot
            && Candidates(context).Any(text => TriggerAt(context, place, text) is not null);
    }

    /// <summary>Puts the wrapper on the node the caret is at the edge of, if the language declares one there.</summary>
    /// <returns>The applied result, or <see langword="null"/> when no trigger claims this keystroke.</returns>
    /// <remarks>
    /// The text is written into the wrapper only when the property the trigger names accepts it, and it is
    /// dropped rather than refused otherwise - a mistake in a declared trigger must not be able to throw from a
    /// keystroke, and "the right concept appeared, with the operator missing" is a visible diagnostic.
    /// </remarks>
    private EditResult? Trigger(EditContext context, CaretPlace place, Selection selection)
    {
        if (!selection.IsEmpty)
        {
            return null;
        }

        // The whole word spelled at this edge first, then the burst that just arrived. A keyword only means
        // something once its last letter is there, while an operator has to work even after a letter that meant
        // nothing was typed before it.
        foreach (var text in Candidates(context))
        {
            if (TriggerAt(context, place, text) is not { } found)
            {
                continue;
            }

            var (node, triggered) = found;

            var wrapper = context.WrapChild(
                node,
                triggered.Concept.Key,
                triggered.Trigger.AnchorSlot,
                PresetsOf(triggered, text));

            return EditResult.Applied(Selection.At(ContinueAt(wrapper, triggered.Trigger)));
        }

        return null;
    }

    /// <summary>The texts a trigger may be asked about, longest first.</summary>
    /// <remarks>
    /// The accumulated word and then the burst on its own. Without the second, one character that meant nothing
    /// would block every later gesture at that edge - typing <c>x</c> and then <c>+</c> after a number has to give
    /// an addition, not a sequence nobody claims.
    /// </remarks>
    private IEnumerable<string> Candidates(EditContext context)
    {
        if (context.PendingText is { Length: > 0 } pending)
        {
            var word = string.Concat(pending, Text);

            yield return word;

            if (!word.Equals(Text, StringComparison.Ordinal))
            {
                yield return Text;
            }

            yield break;
        }

        yield return Text;
    }

    /// <summary>The trigger this keystroke fires, and the node it would go around.</summary>
    /// <param name="context">The apply in progress.</param>
    /// <param name="place">Where the caret stands.</param>
    /// <remarks>
    /// Candidates are tried in the order the anchors answer - a single-cell node offers its right end first -
    /// and within one candidate the language's declaration order decides, so the answer is always the first
    /// trigger that matches rather than a vote between them.
    /// </remarks>
    private static (Node Node, TriggeredConcept Triggered)? TriggerAt(EditContext context, CaretPlace place, string text)
    {
        foreach (var anchor in TriggerAnchors.Of(context, place, context.Selection.Caret.Offset))
        {
            if (TriggerFor(context, anchor, text) is { } triggered)
            {
                return (anchor.Node, triggered);
            }
        }

        return null;
    }

    /// <summary>The first concept declared for <paramref name="anchor"/> that claims this text.</summary>
    /// <param name="context">The apply in progress.</param>
    /// <param name="anchor">The node and the end of it the caret stands at.</param>
    /// <remarks>
    /// Concepts come in declaration order, so the first match is the language's own answer when two of them claim
    /// the same character on the same side. A trigger whose concept could not stand where the anchor stands is
    /// skipped rather than tried: the model refuses such a wrap, and checking first makes a
    /// declared-but-impossible gesture a keystroke that does nothing instead of an editor that throws.
    /// </remarks>
    private static TriggeredConcept? TriggerFor(EditContext context, TriggerAnchor anchor, string text)
    {
        if (anchor.Node.Parent is not { } parent || anchor.Node.ParentSlot is not { } slot)
        {
            // A root has no place, so there is nothing that could hold the wrapper.
            return null;
        }

        foreach (var triggered in context.Concepts.TriggersFor(anchor.Node.Concept))
        {
            if (triggered.Trigger.Side == anchor.Side
                && triggered.Trigger.Claims(text)
                && Fits(parent, slot, triggered))
            {
                return triggered;
            }
        }

        return null;
    }

    /// <summary>Whether the concept a trigger creates is a wrap the model would accept here.</summary>
    /// <remarks>
    /// Only the two questions registration could not ask: whether the wrapper conforms to the slot the anchor
    /// stands in, and whether <see cref="Applicability"/> allows it there. Everything else - the anchor slot is a
    /// single child, the anchor conforms to it, the property and caret slots exist, the concept is concrete - was
    /// checked when the language was registered.
    /// </remarks>
    private static bool Fits(Node parent, string slot, TriggeredConcept triggered) =>
        parent.Concept.FindChild(slot) is { } place
        && triggered.Concept.IsKindOf(place.ChildConceptKey)
        && triggered.Concept.Applicability.Allows(parent.Concept, slot);

    /// <summary>The values the wrapper is born with: its fixed ones, and the typed text where the trigger says so.</summary>
    /// <param name="triggered">The concept that would be created and the declaration that claims the keystroke.</param>
    /// <param name="text">The text that was recognised, which may be a whole word rather than one keystroke.</param>
    /// <remarks>
    /// A preset wins over the typed text where both name one slot, because a language that spells a value out
    /// meant it: <c>and</c> becomes <c>&amp;&amp;</c> even though the word is what the user typed. The text is
    /// written only when the property's type accepts it - a keystroke must not be able to throw over a trigger
    /// that claims more than its concept can store.
    /// </remarks>
    private static Dictionary<string, string>? PresetsOf(TriggeredConcept triggered, string text)
    {
        Dictionary<string, string>? presets = null;

        foreach (var (slot, value) in triggered.Trigger.Presets)
        {
            if (triggered.Concept.FindProperty(slot) is { } property && property.Type.IsValid(value))
            {
                presets ??= new Dictionary<string, string>(StringComparer.Ordinal);
                presets[slot] = value;
            }
        }

        if (triggered.Trigger.PropertySlot is { } typed
            && !(presets?.ContainsKey(typed) ?? false)
            && triggered.Concept.FindProperty(typed) is { } target
            && target.Type.IsValid(text))
        {
            presets ??= new Dictionary<string, string>(StringComparer.Ordinal);
            presets[typed] = text;
        }

        return presets;
    }

    /// <summary>Where the caret goes once the wrapper exists.</summary>
    /// <remarks>
    /// The trigger's own slot when it names one, which is how <c>5</c> followed by <c>+</c> leaves the caret in
    /// the right operand. Otherwise the caret stays inside the node that was wrapped, so a prefix operator such
    /// as <c>!</c> does not move the user away from the condition they were writing.
    /// </remarks>
    private static CaretPosition ContinueAt(Node wrapper, ConceptTrigger trigger) =>
        trigger.CaretSlot is { } slot
            ? CaretPosition.At(CellAddress.OfSlot(wrapper.Id, slot))
            : CaretPosition.At(CellAddress.OfElement(wrapper.Id, trigger.AnchorSlot, 0));

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
            || FindClaimant(context, owner, slot, declared.ChildConceptKey) is not { } rule)
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
    private TypingRule? FindClaimant(EditContext context, Node owner, string slot, string declaredKey)
    {
        foreach (var concept in context.Concepts.Concepts)
        {
            if (!concept.IsAbstract
                && concept.IsKindOf(declaredKey)
                && concept.Applicability.Allows(owner.Concept, slot)
                && context.TypingRules.ClaimFor(concept.Key, Text) is { } rule)
            {
                return rule;
            }
        }

        return null;
    }
}
