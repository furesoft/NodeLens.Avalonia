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
    /// <remarks>
    /// The framework's own vocabulary (<see cref="FrameworkInterfaces.All"/>) is added to the set before it is
    /// resolved, so every registry contains it - a language package's, a project folder's, a test's. That is why a
    /// registry is not literally the list it was given: a language cannot ship without
    /// <see cref="FrameworkInterfaces.Named"/>, and a host can rely on that key meaning the same thing in every
    /// document it opens. Declaring the key yourself is refused rather than silently ignored.
    /// </remarks>
    public static ConceptRegistry Create(IEnumerable<ConceptDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        var declared = new Dictionary<string, ConceptDeclaration>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var declaration in declarations)
        {
            ArgumentNullException.ThrowIfNull(declaration);

            if (FrameworkInterfaces.All.Any(framework => framework.Key == declaration.Key))
            {
                throw new ConceptDefinitionException(
                    $"Concept '{declaration.Key}' is the framework's own interface, so a language may implement it but not declare it.");
            }

            if (!declared.TryAdd(declaration.Key, declaration))
            {
                throw new ConceptDefinitionException($"Concept '{declaration.Key}' is declared more than once.");
            }

            order.Add(declaration.Key);
        }

        // First, so that a language's own order is untouched below them and a reader of Concepts meets the
        // vocabulary before the languages that use it. They are abstract and not offered in completion, so
        // nothing a user creates moves because of the position.
        order.InsertRange(0, FrameworkInterfaces.All.Select(declaration => declaration.Key));

        foreach (var framework in FrameworkInterfaces.All)
        {
            declared.Add(framework.Key, framework);
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

            var interfaces = new Concept[declaration.Interfaces.Count];
            for (var i = 0; i < interfaces.Length; i++)
            {
                interfaces[i] = Resolve(declaration.Interfaces[i]);
            }

            var concept = Build(declaration, [.. supers, .. interfaces], supers, interfaces);
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

        foreach (var concept in ordered)
        {
            foreach (var place in concept.Applicability.Places)
            {
                if (!byKey.TryGetValue(place.ConceptKey, out var host))
                {
                    throw new ConceptDefinitionException(
                        $"Concept '{concept.Key}' may stand in '{place}', which is never declared.");
                }

                if (place.Slot is not { } slot)
                {
                    continue;
                }

                if (host.FindChild(slot) is not { } child)
                {
                    throw new ConceptDefinitionException(
                        $"Concept '{concept.Key}' may stand in '{place}', but '{host.Key}' declares no child slot named '{slot}'.");
                }

                if (!concept.IsKindOf(child.ChildConceptKey))
                {
                    throw new ConceptDefinitionException(
                        $"Concept '{concept.Key}' may stand in '{place}', but that slot holds '{child.ChildConceptKey}'.");
                }
            }
        }

        foreach (var concept in ordered)
        {
            ValidateTriggers(concept, byKey);
        }

        return new ConceptRegistry(byKey, ordered);
    }

    /// <summary>
    /// Checks that every gesture a concept declares could actually run, which is where a typo belongs.
    /// </summary>
    /// <remarks>
    /// A trigger names slots of the concept that declares it and an anchor concept that lives somewhere else, so
    /// none of it is checked by the compiler. Doing it here turns "the keystroke does nothing" into a language
    /// error at load, which is the same trade <see cref="NotationRegistry.Add"/> makes for a notation.
    /// </remarks>
    private static void ValidateTriggers(Concept concept, Dictionary<string, Concept> byKey)
    {
        foreach (var trigger in concept.Triggers)
        {
            if (!byKey.TryGetValue(trigger.AnchorConcept, out var anchor))
            {
                throw new ConceptDefinitionException(
                    $"Concept '{concept.Key}' has a trigger for '{trigger.AnchorConcept}', which is never declared.");
            }

            if (concept.FindChild(trigger.AnchorSlot) is not { Cardinality: Cardinality.One } inner)
            {
                throw new ConceptDefinitionException(
                    $"Concept '{concept.Key}' has a trigger whose anchor slot '{trigger.AnchorSlot}' is not a single-child slot it declares.");
            }

            if (!anchor.IsKindOf(inner.ChildConceptKey))
            {
                throw new ConceptDefinitionException(
                    $"Concept '{concept.Key}' has a trigger for '{trigger.AnchorConcept}', but its slot '{trigger.AnchorSlot}' holds '{inner.ChildConceptKey}'.");
            }

            if (trigger.PropertySlot is { } property && concept.FindProperty(property) is null)
            {
                throw new ConceptDefinitionException(
                    $"Concept '{concept.Key}' has a trigger that writes into '{property}', which is not a property it declares.");
            }

            if (trigger.CaretSlot is { } caret && concept.FindChild(caret) is null)
            {
                throw new ConceptDefinitionException(
                    $"Concept '{concept.Key}' has a trigger whose caret slot '{caret}' is not a child slot it declares.");
            }

            foreach (var slot in trigger.Presets.Keys)
            {
                if (concept.FindProperty(slot) is null)
                {
                    throw new ConceptDefinitionException(
                        $"Concept '{concept.Key}' has a trigger with a preset for '{slot}', which is not a property it declares.");
                }
            }

            if (concept.IsAbstract)
            {
                throw new ConceptDefinitionException(
                    $"Concept '{concept.Key}' is abstract and has a trigger, but no node of an abstract concept can exist.");
            }
        }
    }

    /// <summary>
    /// The concepts a keystroke at an edge of <paramref name="anchor"/> could create, in declaration order.
    /// </summary>
    /// <param name="anchor">The concept of the node the caret stands at the edge of.</param>
    /// <returns>
    /// Every trigger declared for that concept or for one of its super concepts, paired with the concept it would
    /// create. Empty when the language has no gesture for this node.
    /// </returns>
    /// <remarks>
    /// Concept declaration order is the tie-break when two concepts claim the same character on the same side, and
    /// it is the same order that ranks completion entries and the default fill: the language author's answer to
    /// "what is the user most likely to want here". A language that wants a narrower reading to win declares that
    /// concept first.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="anchor"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<TriggeredConcept> TriggersFor(Concept anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var found = new List<TriggeredConcept>();

        foreach (var concept in _ordered)
        {
            foreach (var trigger in concept.Triggers)
            {
                if (anchor.IsKindOf(trigger.AnchorConcept))
                {
                    found.Add(new TriggeredConcept(concept, trigger));
                }
            }
        }

        return found;
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
        return DefaultInstantiableFor(conceptKey, parent: null, slot: null);
    }

    /// <summary>
    /// What to create when filling <paramref name="slot"/> of <paramref name="parent"/>, skipping a concept
    /// that may not stand there.
    /// </summary>
    /// <param name="conceptKey">The concept the slot is declared over.</param>
    /// <param name="parent">The node that would hold it, or <see langword="null"/> for a model root.</param>
    /// <param name="slot">That node's child slot, or <see langword="null"/> for a model root.</param>
    /// <returns>
    /// The first concrete concept below <paramref name="conceptKey"/>, in declaration order, that
    /// <see cref="Applicability"/> allows there, or <see langword="null"/> when none does.
    /// </returns>
    /// <remarks>
    /// The context is why this exists: a concept can be a legal child of a slot by type and still not belong in
    /// *this* slot - an attribute in a place the language forbids. Filling an empty slot has to skip it and take
    /// the next candidate rather than create something the model would then refuse.
    /// </remarks>
    public Concept? DefaultInstantiableFor(string conceptKey, Concept? parent, string? slot)
    {
        if (!TryGet(conceptKey, out var requested))
        {
            return null;
        }

        if (!requested.IsAbstract && requested.Applicability.Allows(parent, slot))
        {
            return requested;
        }

        foreach (var candidate in _ordered)
        {
            if (!candidate.IsAbstract
                && candidate.IsKindOf(conceptKey)
                && candidate.Applicability.Allows(parent, slot))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Concept Build(
        ConceptDeclaration declaration,
        Concept[] supers,
        Concept[] extensions,
        Concept[] interfaces)
    {
        ValidateInterface(declaration, extensions, interfaces);

        var slots = new List<SlotDeclaration>();
        var byName = new Dictionary<string, SlotDeclaration>(StringComparer.Ordinal);

        void Merge(SlotDeclaration slot, bool isOwn)
        {
            if (byName.TryGetValue(slot.Name, out var existing))
            {
                if (!HasSameShape(existing, slot))
                {
                    throw new ConceptDefinitionException(
                        $"Concept '{declaration.Key}' inherits two incompatible declarations for slot '{slot.Name}': {existing} and {slot}.");
                }

                // The more derived declaration wins and keeps the first-seen position: the concept's own beats
                // every super's, and between two supers the first in declaration order wins. Metadata only -
                // a default value or a description is a language's decision, while a shape is the contract.
                // Never move the slot, because slot order is part of the saved bytes (ADR 0005).
                if (isOwn)
                {
                    slots[slots.IndexOf(existing)] = slot;
                    byName[slot.Name] = slot;
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
                Merge(slot, isOwn: false);
            }
        }

        foreach (var slot in declaration.Slots)
        {
            Merge(slot, isOwn: true);
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

        // The same walk as the name slot, and for the same reason: a concept that is foldable stays
        // foldable below itself, and a language re-declares the designation only to override it.
        PropertySlot? collapseSlot = null;
        var collapseSlotName = declaration.CollapseSlot;
        if (collapseSlotName is null)
        {
            collapseSlotName = supers.Select(super => super.CollapseSlot?.Name).FirstOrDefault(name => name is not null);
        }

        if (collapseSlotName is { } resolvedCollapseSlot)
        {
            if (!byName.TryGetValue(resolvedCollapseSlot, out var slot))
            {
                throw new ConceptDefinitionException(
                    $"Concept '{declaration.Key}' names '{resolvedCollapseSlot}' as its collapse slot, but no such slot is declared.");
            }

            if (slot is not PropertySlot { Type: var collapseType } collapseProperty
                || !collapseType.Equals(PropertyType.Boolean))
            {
                throw new ConceptDefinitionException(
                    $"Concept '{declaration.Key}' names '{resolvedCollapseSlot}' as its collapse slot, but a collapse slot has to be a boolean property.");
            }

            collapseSlot = collapseProperty;
        }

        return new Concept(
            declaration.Key,
            supers,
            slots,
            nameSlot,
            declaration.IsAbstract,
            declaration.IsOfferedInCompletion,
            declaration.Description,
            collapseSlot,
            declaration.Applicability,
            declaration.Triggers,
            declaration.IsInterface,
            extensions,
            interfaces);
    }

    /// <summary>
    /// Whether two declarations of one slot agree on what the slot <em>is</em>, which is the part an inheritance
    /// cannot renegotiate.
    /// </summary>
    /// <remarks>
    /// Shape is the slot kind and everything that decides what may stand in it: a property's type, a reference's
    /// target and subtree permission, a child slot's concept, cardinality and minimum. Metadata is what a more
    /// derived declaration may override without changing the contract - a default value, a description, whether a
    /// filled value shows it - because a role cannot choose a placeholder that suits every language.
    /// </remarks>
    private static bool HasSameShape(SlotDeclaration left, SlotDeclaration right) => (left, right) switch
    {
        (PropertySlot a, PropertySlot b) => a.Type.Equals(b.Type),
        (ReferenceSlot a, ReferenceSlot b) => a.TargetConceptKey == b.TargetConceptKey
            && a.AllowOwnSubtree == b.AllowOwnSubtree,
        (ChildSlot a, ChildSlot b) => a.ChildConceptKey == b.ChildConceptKey
            && a.Cardinality == b.Cardinality
            && a.Minimum == b.Minimum,
        _ => false,
    };

    /// <summary>
    /// Refuses an interface that would make the flat slot map or the aspect walk say something it cannot mean.
    /// </summary>
    /// <remarks>
    /// These are the rules a single declaration cannot check, because each one needs the graph: what a super is,
    /// and whether the thing declaring a member is a concept that could ever be a node.
    /// </remarks>
    private static void ValidateInterface(ConceptDeclaration declaration, Concept[] extensions, Concept[] interfaces)
    {
        if (declaration.IsInterface)
        {
            // A child slot would make every implementer's notation owe a cell it never asked for, and a
            // registered concept without a notation is the one state a user cannot work around.
            if (declaration.Slots.Any(slot => slot is ChildSlot))
            {
                throw new ConceptDefinitionException(
                    $"Interface '{declaration.Key}' declares a child slot, but an interface contributes properties and references only.");
            }

            if (declaration.Triggers.Count > 0)
            {
                throw new ConceptDefinitionException(
                    $"Interface '{declaration.Key}' declares a trigger, but no node is ever an interface, so no gesture could edge one.");
            }

            if (!declaration.Applicability.Equals(Applicability.Anywhere))
            {
                throw new ConceptDefinitionException(
                    $"Interface '{declaration.Key}' names a place, but an interface is never a node, so it stands nowhere.");
            }
        }

        foreach (var extension in extensions)
        {
            if (extension.IsInterface)
            {
                throw new ConceptDefinitionException(
                    $"Concept '{declaration.Key}' extends '{extension.Key}', which is an interface - implement it instead, which is the same conformance with a name that says why.");
            }
        }

        foreach (var implemented in interfaces)
        {
            if (!implemented.IsInterface)
            {
                throw new ConceptDefinitionException(
                    $"Concept '{declaration.Key}' implements '{implemented.Key}', which is not an interface - declare it as a super concept instead.");
            }
        }
    }
}
