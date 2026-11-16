using System.Diagnostics.CodeAnalysis;

namespace Projectional.Core;

/// <summary>
/// A concept as the runtime sees it: super concepts resolved and the full inherited slot set
/// flattened into one ordered list. Instances come from <see cref="ConceptRegistry"/>.
/// </summary>
public sealed class Concept
{
    private readonly Dictionary<string, SlotDeclaration> _slotsByName;
    private readonly HashSet<string> _ancestry;

    internal Concept(
        string key,
        IReadOnlyList<Concept> superConcepts,
        IReadOnlyList<SlotDeclaration> slots,
        PropertySlot? nameSlot,
        bool isAbstract,
        bool isOfferedInCompletion,
        string? description = null,
        PropertySlot? collapseSlot = null,
        Applicability? applicability = null)
    {
        Key = key;
        SuperConcepts = superConcepts;
        Slots = slots;
        NameSlot = nameSlot;
        IsAbstract = isAbstract;
        IsOfferedInCompletion = isOfferedInCompletion;
        Description = description;
        CollapseSlot = collapseSlot;
        Applicability = applicability ?? Applicability.Anywhere;
        _slotsByName = new Dictionary<string, SlotDeclaration>(slots.Count, StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            _slotsByName.Add(slot.Name, slot);
        }

        _ancestry = new HashSet<string>(StringComparer.Ordinal) { key };
        foreach (var super in superConcepts)
        {
            _ancestry.UnionWith(super._ancestry);
        }
    }

    public string Key { get; }

    /// <summary>Direct super concepts, in declaration order.</summary>
    public IReadOnlyList<Concept> SuperConcepts { get; }

    /// <summary>
    /// The flat slot set: inherited slots first, then own ones, each in declaration order.
    /// This order is also the order the persistence format writes slots in.
    /// </summary>
    public IReadOnlyList<SlotDeclaration> Slots { get; }

    /// <summary>The string property that references to this concept display, if declared.</summary>
    public PropertySlot? NameSlot { get; }

    /// <summary>
    /// Whether this concept only exists to be extended, so that no node is ever an instance of it.
    /// </summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// Whether the editor may offer this concept when the user asks what can stand in a slot. It is a
    /// statement about the menu, not about the model: the concept stays a legal child of every slot
    /// that accepts it.
    /// </summary>
    public bool IsOfferedInCompletion { get; }

    /// <summary>
    /// The one sentence of documentation this concept declares, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Not inherited: <see cref="SuperConcepts"/> is walked for notation and for slots, but a sentence
    /// describes the concept it was written on. It is documentation rather than model data, so it is
    /// never stored per node and never appears in a change event.
    /// </remarks>
    public string? Description { get; }

    /// <summary>
    /// The resolved slot that says whether a node of this concept may be folded, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Inherited the way <see cref="NameSlot"/> is: a concept that is foldable stays foldable in its
    /// subconcepts, and a language re-declares the designation only to override it. It is a
    /// <see cref="PropertySlot"/> rather than a name because the caller needs the type, and a collapse slot
    /// is always a boolean.
    /// </remarks>
    public PropertySlot? CollapseSlot { get; }

    /// <summary>Where nodes of this concept may stand; <see cref="Core.Applicability.Anywhere"/> when unconstrained.</summary>
    /// <remarks>
    /// A constraint the concept carries rather than one its slots carry, because it is not about the slot: the
    /// same concept may be allowed in one place and forbidden in another of the same type. The model refuses a
    /// placement it forbids, and the editor consults it before offering or creating one.
    /// </remarks>
    public Applicability Applicability { get; }


    /// <summary>Whether this concept is <paramref name="conceptKey"/> itself or extends it.</summary>
    public bool IsKindOf(string conceptKey) => conceptKey is not null && _ancestry.Contains(conceptKey);

    public bool TryGetSlot(string name, [NotNullWhen(true)] out SlotDeclaration? slot)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _slotsByName.TryGetValue(name, out slot);
    }

    public SlotDeclaration GetSlot(string name) => TryGetSlot(name, out var slot)
        ? slot
        : throw new KeyNotFoundException($"Concept '{Key}' declares no slot named '{name}'.");

    public PropertySlot? FindProperty(string name) => Find<PropertySlot>(name);

    public ChildSlot? FindChild(string name) => Find<ChildSlot>(name);

    public ReferenceSlot? FindReference(string name) => Find<ReferenceSlot>(name);

    private TSlot? Find<TSlot>(string name)
        where TSlot : SlotDeclaration =>
        TryGetSlot(name, out var slot) && slot is TSlot typed ? typed : null;

    public override string ToString() => Key;
}
