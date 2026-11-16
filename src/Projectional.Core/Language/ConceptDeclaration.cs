namespace Projectional.Core;

/// <summary>
/// What a language says about one kind of node: its key, the slots it adds, the concepts it
/// extends, and which slot holds the name that references display.
/// </summary>
/// <remarks>
/// Super concepts exist for semantics — aspects, the inherited slot set, and conformance
/// checks. They deliberately do not shape storage: a node's data is always one flat slot map.
/// </remarks>
public sealed class ConceptDeclaration
{
    public ConceptDeclaration(
        string key,
        IEnumerable<SlotDeclaration>? slots = null,
        IEnumerable<string>? superConcepts = null,
        string? nameSlot = null,
        bool isAbstract = false,
        bool isOfferedInCompletion = true,
        string? description = null,
        string? collapseSlot = null,
        Applicability? applicability = null,
        IEnumerable<ConceptTrigger>? triggers = null)
    {
        if (!NameRules.IsValidConceptKey(key))
        {
            throw new ArgumentException($"'{key}' is not a valid concept key.", nameof(key));
        }

        var slotList = new List<SlotDeclaration>();
        var slotNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots ?? [])
        {
            ArgumentNullException.ThrowIfNull(slot);

            if (!slotNames.Add(slot.Name))
            {
                throw new ArgumentException(
                    $"Concept '{key}' declares slot '{slot.Name}' more than once.",
                    nameof(slots));
            }

            slotList.Add(slot);
        }

        var superList = new List<string>();
        foreach (var super in superConcepts ?? [])
        {
            if (!NameRules.IsValidConceptKey(super))
            {
                throw new ArgumentException($"'{super}' is not a valid concept key.", nameof(superConcepts));
            }

            if (super == key)
            {
                throw new ArgumentException($"Concept '{key}' cannot be its own super concept.", nameof(superConcepts));
            }

            if (!superList.Contains(super, StringComparer.Ordinal))
            {
                superList.Add(super);
            }
        }

        if (nameSlot is not null && !NameRules.IsValidSlotName(nameSlot))
        {
            throw new ArgumentException($"'{nameSlot}' is not a valid slot name.", nameof(nameSlot));
        }

        if (collapseSlot is not null && !NameRules.IsValidSlotName(collapseSlot))
        {
            throw new ArgumentException($"'{collapseSlot}' is not a valid slot name.", nameof(collapseSlot));
        }

        Key = key;
        Slots = slotList;
        SuperConcepts = superList;
        NameSlot = nameSlot;
        IsAbstract = isAbstract;
        IsOfferedInCompletion = isOfferedInCompletion;
        CollapseSlot = collapseSlot;
        Applicability = applicability ?? Applicability.Anywhere;
        Triggers = [.. triggers ?? []];

        // Trimmed, and an empty one is no description at all: "declared nothing" and "declared a blank"
        // have to be one state, or a language ships a tooltip that shows nothing.
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public string Key { get; }

    /// <summary>Slots declared directly on this concept, in declaration order.</summary>
    public IReadOnlyList<SlotDeclaration> Slots { get; }

    /// <summary>Direct super concepts, in declaration order.</summary>
    public IReadOnlyList<string> SuperConcepts { get; }

    /// <summary>The slot that supplies the text a reference to this concept displays.</summary>
    public string? NameSlot { get; }

    /// <summary>
    /// Whether this concept exists only to be extended. An abstract concept can declare slots and be
    /// named as a slot's content, but no node is ever an instance of it: it stands for "one of the
    /// concepts below me", the way a base type does in a typed language.
    /// </summary>
    /// <remarks>
    /// Without this, a slot that accepts several kinds of thing has to be declared once per kind or
    /// has to accept a concept that is meaningless on its own — and <see cref="Model.NewChild"/>
    /// would happily create that meaningless node.
    /// </remarks>
    public bool IsAbstract { get; }

    /// <summary>
    /// Whether the editor may offer this concept when the user asks what can stand in a slot.
    /// </summary>
    /// <remarks>
    /// A language hides the concepts a user reaches for by typing rather than by picking from a
    /// menu — a literal is the obvious case, because "an integer literal" is not something anyone
    /// wants to read in a list of eight entries. Hiding one only silences the list: the concept is
    /// still a legal child of every slot that accepts it, and it can still be what
    /// <see cref="ConceptRegistry.DefaultInstantiableFor"/> materializes when the user presses Enter
    /// in an empty slot. Otherwise a language would have no way left to create it.
    /// </remarks>
    public bool IsOfferedInCompletion { get; }

    /// <summary>
    /// One sentence of documentation about this concept, shown where the concept is met: on hover over a
    /// node of it and beside the completion entry that offers it. <see langword="null"/> when the language
    /// declared none.
    /// </summary>
    /// <remarks>
    /// Language data, not model data: it is not persisted per node, creates no undo step and is not
    /// inherited - a super concept's sentence describes the super concept, not the node the user is
    /// looking at. A surface shows nothing at all when it is absent, because an empty box that repeats
    /// the concept key teaches the user that hovering is pointless.
    /// </remarks>
    public string? Description { get; }

    /// <summary>
    /// The slot that says whether a node of this concept may be folded in the editor, or
    /// <see langword="null"/> when the concept does not opt in.
    /// </summary>
    /// <remarks>
    /// A designated slot like <see cref="NameSlot"/>: the permission is a value on the node, so two nodes
    /// of one concept can differ - one rule's body is worth hiding and its sibling's is not. A concept that
    /// never nominates one behaves exactly as it did before folding existed.
    /// </remarks>
    public string? CollapseSlot { get; }

    /// <summary>
    /// Where nodes of this concept may stand, or <see cref="Applicability.Anywhere"/> when the language
    /// constrains nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the concepts that are not valid wherever their type is accepted: an attribute on a class, a method or
    /// a parameter and nowhere else, an XML comment only in front of a class or a method. Without it the editor
    /// offers such a concept in slots that would reject it, and the model accepts a node the language forbids
    /// ([Applicability](../architecture/language.md#applicability)).
    /// </para>
    /// <para>
    /// A place names a concept and, optionally, one slot of it; naming a super concept is how "anywhere inside a
    /// declaration" is written. The registry checks every place at registration - the concept must exist, and a
    /// named slot must hold children that accept this concept - so a typo is a language error at load, not a
    /// keystroke that does nothing.
    /// </para>
    /// </remarks>
    public Applicability Applicability { get; }

    /// <summary>
    /// The gestures that create this concept by typing at the edge of a node that already exists, in declaration
    /// order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Empty is the normal case: a concept is created from completion or from an empty slot unless the language
    /// says that a character at a neighbouring node means it. <c>5</c> followed by <c>+</c> is the reference
    /// case - the property refuses the character, and this list is what makes it an addition instead
    /// ([triggers.md](../architecture/triggers.md)).
    /// </para>
    /// <para>
    /// <b>Not inherited.</b> A trigger creates the concept it is declared on, so a subtype reusing its super
    /// concept's trigger would create the super concept. A language that wants the gesture below itself declares
    /// it again, which is also what makes the intent visible.
    /// </para>
    /// <para>
    /// The registry checks every trigger at registration - the anchor concept exists, the anchor slot is a
    /// single child of this concept and accepts the anchor, the property and caret slots exist, and the concept
    /// is concrete - so a typo is a language error at load rather than a keystroke that silently does nothing.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ConceptTrigger> Triggers { get; }

    public override string ToString() => Key;
}
