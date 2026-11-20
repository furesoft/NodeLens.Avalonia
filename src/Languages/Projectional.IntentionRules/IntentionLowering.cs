using Projectional.Core;
using Projectional.Editing;

namespace Projectional.IntentionRules;

/// <summary>Turns written intention rules into the <see cref="IIntentionProvider"/> a session registers.</summary>
/// <remarks>
/// <para>
/// This is the half that makes the declared path equal rather than weaker (ADR 0014): a rule becomes an
/// <see cref="Intention"/> whose <c>Create</c> answers the very action a hand-written package would answer, so there
/// is no second apply path and no declared-only behaviour.
/// </para>
/// <para>
/// It checks what a compiler would have checked: the concept exists, the body is one of the two kinds, a wrapper
/// exists and has the single-child slot the rule names, that slot accepts the node's concept (when the rule named
/// one), every preset names a property of the wrapper, and a written property exists on the concept. What it cannot
/// know - whether this particular node stands where the wrapper fits - is the action's refusal, which is the same
/// refusal the C# entry gets.
/// </para>
/// </remarks>
public static class IntentionLowering
{
    /// <summary>The intentions a language's intention documents declare.</summary>
    /// <param name="documents">
    /// The roots of those documents: one <see cref="IntentionConceptKeys.Rule"/> per document, in file order.
    /// </param>
    /// <param name="concepts">The concepts those rules may name, which is the language's own registry.</param>
    /// <returns>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A document cannot be lowered.</exception>
    public static IIntentionProvider CreateProvider(IEnumerable<Node> documents, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(concepts);

        var entries = new List<DeclaredIntention>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in documents)
        {
            if (rule.Concept.Key != IntentionConceptKeys.Rule)
            {
                throw new ConceptDefinitionException(
                    $"'{rule.Concept.Key}' is not an intention; an intentions document holds one '{IntentionConceptKeys.Rule}'.");
            }

            var label = Required(rule, IntentionLanguage.LabelSlot, "An intention has to say what the list shows.");
            var conceptKey = Optional(rule, IntentionLanguage.ConceptKeySlot);

            Concept? subject = null;

            if (conceptKey is not null && !concepts.TryGet(conceptKey, out subject))
            {
                throw new ConceptDefinitionException(
                    $"An intention names '{conceptKey}', which is not a concept of this language; leave the concept empty to offer it on every node.");
            }

            var id = $"intent.{conceptKey ?? "any"}.{label.Replace(' ', '-')}";

            if (!ids.Add(id))
            {
                throw new ConceptDefinitionException(
                    $"Two intentions are called '{label}' on '{conceptKey ?? "any node"}'; one entry per label and concept.");
            }

            var description = Optional(rule, IntentionLanguage.DescriptionSlot)
                ?? $"Runs '{label}'.";

            var action = Single(rule, IntentionLanguage.ActionSlot);

            var safety = action.Concept.Key is IntentionConceptKeys.Delete or IntentionConceptKeys.Replace or IntentionConceptKeys.Inline
                ? EditSafety.Destructive
                : EditSafety.Plain;

            // An insert only means something on an empty slot; anywhere else the offer would come back rejected.
            Func<IntentionContext, bool>? place = action.Concept.Key == IntentionConceptKeys.Insert
                ? context => context.Place is { } at && at.IsEmptySlot
                : null;

            entries.Add(new DeclaredIntention(
                new Intention(id, label, description, Create(action, label, subject, concepts), safety: safety),
                conceptKey,
                place));
        }

        return new DeclaredIntentions(entries);
    }

    /// <summary>The action a body stands for, built when the intention runs.</summary>
    private static Func<IntentionContext, EditAction?> Create(
        Node action,
        string label,
        Concept? subject,
        ConceptRegistry concepts) => action.Concept.Key switch
        {
            IntentionConceptKeys.Wrap => Wrap(action, subject, concepts),
            IntentionConceptKeys.Set => Set(action, subject),
            IntentionConceptKeys.InsertSibling => InsertSibling(action),
            IntentionConceptKeys.Delete => Delete(),
            IntentionConceptKeys.Move => Move(action),
            IntentionConceptKeys.Replace => Replace(action, subject, concepts),
            IntentionConceptKeys.Inline => Inline(action, subject),
            IntentionConceptKeys.Insert => Insert(action, concepts),
            var other => throw new ConceptDefinitionException(
                $"'{other}' is not an intention body; an intention wraps or sets."),
        };

    private static Func<IntentionContext, EditAction?> Wrap(Node action, Concept? subject, ConceptRegistry concepts)
    {
        var wrapperKey = Required(action, IntentionLanguage.WrapperSlot, "A wrap has to name the wrapper concept.");
        var childSlot = Required(action, IntentionLanguage.WrapperChildSlot, $"A wrap has to name the slot of '{wrapperKey}' that receives the node.");

        if (!concepts.TryGet(wrapperKey, out var wrapper))
        {
            throw new ConceptDefinitionException(
                $"A wrap names '{wrapperKey}', which is not a concept of this language.");
        }

        if (!wrapper.Slots.OfType<ChildSlot>().Any(slot => slot.Name == childSlot && slot.Cardinality == Cardinality.One))
        {
            throw new ConceptDefinitionException(
                $"'{wrapperKey}' declares no single-child slot named '{childSlot}'; a wrapper has one place for the node.");
        }

        var receiving = wrapper.Slots.OfType<ChildSlot>().First(slot => slot.Name == childSlot);

        if (subject is not null && !subject.IsKindOf(receiving.ChildConceptKey))
        {
            throw new ConceptDefinitionException(
                $"'{wrapperKey}.{childSlot}' does not accept '{subject.Key}', so the wrap could never run.");
        }

        var presets = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var preset in action.GetChildren(IntentionLanguage.PresetSlot))
        {
            var property = Required(preset, IntentionLanguage.PresetPropertySlot, "A preset has to name a property.");
            var value = Required(preset, IntentionLanguage.PresetValueSlot, $"The preset '{property}' has to say what the wrapper starts with.");

            if (!wrapper.Slots.OfType<PropertySlot>().Any(slot => slot.Name == property))
            {
                throw new ConceptDefinitionException(
                    $"'{wrapperKey}' declares no property named '{property}', so a preset cannot set it.");
            }

            if (!presets.TryAdd(property, value))
            {
                throw new ConceptDefinitionException(
                    $"Two presets of '{wrapperKey}' set '{property}'; the wrapper starts with one value.");
            }
        }

        // The wrapper's own key is resolved when the intention runs: a rule names concepts, and the node it wraps is
        // whatever the caret stands on, which no rule can know at load.
        return context => context.Node is { } node
            ? new WrapInAction(node.Id, wrapperKey, childSlot, presets)
            : null;
    }

    private static Func<IntentionContext, EditAction?> Insert(Node action, ConceptRegistry concepts)
    {
        var conceptKey = Required(action, IntentionLanguage.ConceptSlot, "An insert has to name the concept to add in the slot.");

        if (!concepts.TryGet(conceptKey, out _))
        {
            throw new ConceptDefinitionException($"An insert names '{conceptKey}', which is not a concept of this language.");
        }

        // The slot is the caret's, not the rule's: "put one of these here" is about the empty place the user is in.
        return context => context.Place is { } place && place.IsEmptySlot
            ? new AddChildAction(place.Address.Owner, place.Address.Slot!, conceptKey)
            : null;
    }

    private static Func<IntentionContext, EditAction?> Replace(Node action, Concept? subject, ConceptRegistry concepts)
    {
        var conceptKey = Required(
            action,
            IntentionLanguage.WithSlot,
            "A replace has to name the concept that takes the place of the node.");

        if (!concepts.TryGet(conceptKey, out var replacement))
        {
            throw new ConceptDefinitionException($"A replace names '{conceptKey}', which is not a concept of this language.");
        }

        if (subject is not null && replacement.Key == subject.Key)
        {
            throw new ConceptDefinitionException(
                $"A replace of '{subject.Key}' names the same concept, so the replacement would be the node again.");
        }

        return context => context.Node is { } node
            ? new ReplaceNodeAction(node.Id, replacement.Key)
            : null;
    }

    /// <summary>Replaces the node with a copy of what its own reference points at.</summary>
    /// <remarks>
    /// <para>
    /// The declaration names the reference slot, because only the language knows which of a node's slots is the one that
    /// stands for "the definition of what I mean". The rest is the framework's: the node at the caret is the use, its
    /// slot names the definition, and the use's own place receives a copy of it - a copy rather than a move, so the
    /// definition stays for every other use.
    /// </para>
    /// <para>
    /// It does <em>not</em> delete a definition nobody references any more. That is an open question of the refactoring
    /// plan, not a detail of this body: deleting is destructive, and a body that quietly tidied up would be the one
    /// place in the framework where an edit removes something the user did not point at.
    /// </para>
    /// </remarks>
    private static Func<IntentionContext, EditAction?> Inline(Node action, Concept? subject)
    {
        var slot = Required(
            action,
            IntentionLanguage.ReferenceSlot,
            "An inline has to name the reference slot that points at the definition.");

        if (subject is not null && !subject.Slots.OfType<ReferenceSlot>().Any(reference => reference.Name == slot))
        {
            throw new ConceptDefinitionException(
                $"An inline of '{subject.Key}' names the reference slot '{slot}', which that concept does not declare.");
        }

        return context =>
        {
            if (context.Node is not { } node
                || node.GetReferenceTarget(slot) is not { } target
                || node.Parent is not { } parent
                || node.ParentSlot is not { } place)
            {
                return null;
            }

            return EditProgram.Of(
                new InsertCopyAction(target.Id, parent.Id, place, node.IndexInParent),
                new DeleteNodeAction(node.Id));
        };
    }

    private static Func<IntentionContext, EditAction?> Delete() =>
        context => context.Node is null ? null : new DeleteNodeAction();

    private static Func<IntentionContext, EditAction?> Move(Node action)
    {
        var move = Optional(action, IntentionLanguage.MoveSlot) switch
        {
            null or IntentionMoves.Up => NodeMove.Up,
            IntentionMoves.Down => NodeMove.Down,
            IntentionMoves.ToFront => NodeMove.ToFront,
            IntentionMoves.ToBack => NodeMove.ToBack,
            var other => throw new ConceptDefinitionException(
                $"'{other}' is not a direction; a move sends the node {IntentionMoves.Up}, {IntentionMoves.Down}, {IntentionMoves.ToFront} or {IntentionMoves.ToBack}."),
        };

        return context => context.Node is null ? null : new MoveNodeAction(move);
    }

    private static Func<IntentionContext, EditAction?> InsertSibling(Node action)
    {
        var place = Optional(action, IntentionLanguage.SideSlot) switch
        {
            null or IntentionSiblings.After => SiblingPlace.After,
            IntentionSiblings.Before => SiblingPlace.Before,
            var other => throw new ConceptDefinitionException(
                $"'{other}' is not a side; an insert puts the sibling {IntentionSiblings.Before} or {IntentionSiblings.After}."),
        };

        return context => context.Node is { } node
            ? new InsertSiblingAction(node.Id, place)
            : null;
    }

    private static Func<IntentionContext, EditAction?> Set(Node action, Concept? subject)
    {
        var slot = Required(action, IntentionLanguage.SetSlot, "A set has to name the property it writes.");
        var value = Required(action, IntentionLanguage.SetValueSlot, $"Writing '{slot}' has to say what to write.");

        if (subject is not null && !subject.Slots.OfType<PropertySlot>().Any(property => property.Name == slot))
        {
            throw new ConceptDefinitionException(
                $"'{subject.Key}' declares no property named '{slot}', so the intention could never write it.");
        }

        return context => context.Node is { } node
            ? new SetNodePropertyAction(node.Id, slot, value)
            : null;
    }

    private static string Required(Node node, string slot, string message) =>
        Optional(node, slot) ?? throw new ConceptDefinitionException(message);

    private static string? Optional(Node node, string slot) =>
        node.GetOrNull(slot) is { Length: > 0 } value ? value : null;

    private static Node Single(Node node, string slot)
    {
        var children = node.GetChildren(slot);

        return children.Count == 1
            ? children[0]
            : throw new ConceptDefinitionException(
                $"An intention needs exactly one body in '{slot}'; it has {children.Count}.");
    }
}
