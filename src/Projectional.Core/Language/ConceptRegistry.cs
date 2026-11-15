namespace Projectional.Core;

/// <summary>
/// The set of concepts a language declares, resolved and validated once so that every lookup
/// afterwards is a plain dictionary read.
/// </summary>
public sealed class ConceptRegistry
{
    private readonly Dictionary<string, Concept> _byKey;
    private readonly IReadOnlyList<Concept> _ordered;

    private ConceptRegistry(Dictionary<string, Concept> byKey, IReadOnlyList<Concept> ordered)
    {
        _byKey = byKey;
        _ordered = ordered;
    }

    /// <summary>All concepts in declaration order, which is the order they are persisted in.</summary>
    public IReadOnlyList<Concept> Concepts => _ordered;

    /// <summary>Resolves and validates a set of declarations.</summary>
    /// <exception cref="ConceptDefinitionException">The declarations do not form a usable language.</exception>
    public static ConceptRegistry Create(IEnumerable<ConceptDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        var declared = new Dictionary<string, ConceptDeclaration>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var declaration in declarations)
        {
            ArgumentNullException.ThrowIfNull(declaration);

            if (!declared.TryAdd(declaration.Key, declaration))
            {
                throw new ConceptDefinitionException($"Concept '{declaration.Key}' is declared more than once.");
            }

            order.Add(declaration.Key);
        }

        var resolved = new Dictionary<string, Concept>(StringComparer.Ordinal);
        var inProgress = new HashSet<string>(StringComparer.Ordinal);

        Concept Resolve(string key)
        {
            if (resolved.TryGetValue(key, out var already))
            {
                return already;
            }

            if (!declared.TryGetValue(key, out var declaration))
            {
                throw new ConceptDefinitionException($"Concept '{key}' is referred to but never declared.");
            }

            if (!inProgress.Add(key))
            {
                throw new ConceptDefinitionException($"Concept '{key}' takes part in an inheritance cycle.");
            }

            var supers = new Concept[declaration.SuperConcepts.Count];
            for (var i = 0; i < supers.Length; i++)
            {
                supers[i] = Resolve(declaration.SuperConcepts[i]);
            }

            var concept = Build(declaration, supers);
            inProgress.Remove(key);
            resolved.Add(key, concept);
            return concept;
        }

        var byKey = new Dictionary<string, Concept>(order.Count, StringComparer.Ordinal);
        var ordered = new List<Concept>(order.Count);
        foreach (var key in order)
        {
            var concept = Resolve(key);
            byKey.Add(key, concept);
            ordered.Add(concept);
        }

        foreach (var concept in ordered)
        {
            foreach (var slot in concept.Slots)
            {
                var target = slot switch
                {
                    ChildSlot child => child.ChildConceptKey,
                    ReferenceSlot reference => reference.TargetConceptKey,
                    _ => null,
                };

                if (target is not null && !byKey.ContainsKey(target))
                {
                    throw new ConceptDefinitionException(
                        $"Concept '{concept.Key}' declares slot '{slot.Name}' of concept '{target}', which is never declared.");
                }
            }
        }

        return new ConceptRegistry(byKey, ordered);
    }

    public Concept Get(string key) => TryGet(key, out var concept)
        ? concept
        : throw new KeyNotFoundException($"No concept is declared under the key '{key}'.");

    public bool TryGet(string? key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Concept? concept)
    {
        if (key is not null)
        {
            return _byKey.TryGetValue(key, out concept);
        }

        concept = null;
        return false;
    }

    /// <summary>
    /// What to create when something asks for a <paramref name="conceptKey"/>: that concept itself
    /// when it can be instantiated, or else the first concept that extends it.
    /// </summary>
    /// <returns><see langword="null"/> when the concept is unknown or every concept that extends it is abstract too.</returns>
    /// <remarks>
    /// Declaration order is the language author's own order, so "first concrete concept below this
    /// one" is a decision a notation author can make by ordering declarations rather than one the
    /// editor has to guess. It is what filling an empty slot does, which is why there has to be an
    /// answer at all: a slot declared over an abstract concept names a set, not a node.
    /// </remarks>
    public Concept? DefaultInstantiableFor(string conceptKey)
    {
        if (!TryGet(conceptKey, out var requested))
        {
            return null;
        }

        if (!requested.IsAbstract)
        {
            return requested;
        }

        foreach (var candidate in _ordered)
        {
            if (!candidate.IsAbstract && candidate.IsKindOf(conceptKey))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Concept Build(ConceptDeclaration declaration, Concept[] supers)
    {
        var slots = new List<SlotDeclaration>();
        var byName = new Dictionary<string, SlotDeclaration>(StringComparer.Ordinal);

        void Merge(SlotDeclaration slot)
        {
            if (byName.TryGetValue(slot.Name, out var existing))
            {
                // The same declaration reached through two super concepts is a diamond, not a
                // conflict. Anything that differs would make the flat slot map ambiguous.
                if (!existing.Equals(slot))
                {
                    throw new ConceptDefinitionException(
                        $"Concept '{declaration.Key}' inherits two different declarations for slot '{slot.Name}'.");
                }

                return;
            }

            byName.Add(slot.Name, slot);
            slots.Add(slot);
        }

        foreach (var super in supers)
        {
            foreach (var slot in super.Slots)
            {
                Merge(slot);
            }
        }

        foreach (var slot in declaration.Slots)
        {
            Merge(slot);
        }

        PropertySlot? nameSlot = null;
        var nameSlotName = declaration.NameSlot;
        if (nameSlotName is null)
        {
            // A concept that is a Named is also named the same way; only declare a name slot
            // again to override it. With several super concepts the first one wins, which
            // keeps the outcome predictable instead of order dependent.
            nameSlotName = supers.Select(super => super.NameSlot?.Name).FirstOrDefault(name => name is not null);
        }

        if (nameSlotName is { } resolvedNameSlot)
        {
            if (!byName.TryGetValue(resolvedNameSlot, out var slot))
            {
                throw new ConceptDefinitionException(
                    $"Concept '{declaration.Key}' names '{resolvedNameSlot}' as its name slot, but no such slot is declared.");
            }

            if (slot is not PropertySlot { Type: var type } property || !type.Equals(PropertyType.String))
            {
                throw new ConceptDefinitionException(
                    $"Concept '{declaration.Key}' names '{resolvedNameSlot}' as its name slot, but a name slot has to be a string property.");
            }

            nameSlot = property;
        }

        return new Concept(declaration.Key, supers, slots, nameSlot, declaration.IsAbstract);
    }
}
