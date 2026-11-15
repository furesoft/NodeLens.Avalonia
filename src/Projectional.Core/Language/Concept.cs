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
        bool isOfferedInCompletion)
    {
        Key = key;
        SuperConcepts = superConcepts;
        Slots = slots;
        NameSlot = nameSlot;
        IsAbstract = isAbstract;
        IsOfferedInCompletion = isOfferedInCompletion;
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
