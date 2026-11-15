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
        bool isOfferedInCompletion = true)
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

        Key = key;
        Slots = slotList;
        SuperConcepts = superList;
        NameSlot = nameSlot;
        IsAbstract = isAbstract;
        IsOfferedInCompletion = isOfferedInCompletion;
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

    public override string ToString() => Key;
}
