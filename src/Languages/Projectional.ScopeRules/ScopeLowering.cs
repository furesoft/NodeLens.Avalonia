using Projectional.Core;
using Projectional.Semantics;

namespace Projectional.ScopeRules;

/// <summary>Turns written scope rules into the <see cref="ScopeProviderRegistry"/> a session installs.</summary>
/// <remarks>
/// <para>
/// This is the half that makes the declared path equal rather than weaker (ADR 0014): every rule becomes a
/// <see cref="NamedScopeProvider"/> entry, so there is no second kind of provider and no place where a declared scope
/// resolves differently from a written one.
/// </para>
/// <para>
/// It checks what a compiler would have checked - the concept exists, the slot is a reference slot that concept
/// declares (inherited ones count), the target concept exists, the place is one of the five symbols, and no two rules
/// answer for the same slot - and it leaves the graph to the registry, which is <see cref="ConceptRegistry.TryGet"/>'s
/// answer. <b>Two rules for one slot are refused rather than merged</b>: a union would silently make one of them
/// unreachable, and a chain would need an order the model never stated.
/// </para>
/// </remarks>
public static class ScopeLowering
{
    /// <summary>The providers a language's scope documents declare.</summary>
    /// <param name="documents">
    /// The roots of those documents: one <see cref="ScopeConceptKeys.Rule"/> per document, in file order.
    /// </param>
    /// <param name="concepts">The concepts those rules may name, which is the language's own registry.</param>
    /// <returns>One provider per concept those rules talk about, or the empty registry when there are none.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ConceptDefinitionException">A document cannot be lowered.</exception>
    public static ScopeProviderRegistry CreateProviders(IEnumerable<Node> documents, ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(concepts);

        var byConcept = new Dictionary<string, Dictionary<string, ScopeBinding>>(StringComparer.Ordinal);

        foreach (var rule in documents)
        {
            if (rule.Concept.Key != ScopeConceptKeys.Rule)
            {
                throw new ConceptDefinitionException(
                    $"'{rule.Concept.Key}' is not a scope rule; a scopes document holds one '{ScopeConceptKeys.Rule}'.");
            }

            var conceptKey = Required(rule, ScopeLanguage.ConceptKeySlot, "A scope rule has to name the concept it is about.");
            var slot = Required(rule, ScopeLanguage.SlotSlot, $"A scope rule has to name the reference slot of '{conceptKey}'.");
            var place = PlaceOf(Required(rule, ScopeLanguage.PlaceSlot, "A scope rule has to name a place."));
            var target = Optional(rule, ScopeLanguage.TargetSlot);

            if (!concepts.TryGet(conceptKey, out var concept))
            {
                throw new ConceptDefinitionException(
                    $"A scope rule names '{conceptKey}', which is not a concept of this language.");
            }

            if (!concept.Slots.OfType<ReferenceSlot>().Any(reference => reference.Name == slot))
            {
                throw new ConceptDefinitionException(
                    $"'{conceptKey}' declares no reference slot named '{slot}'; a scope rule names a reference, not a property or a child.");
            }

            if (target is not null && !concepts.TryGet(target, out _))
            {
                throw new ConceptDefinitionException(
                    $"A scope rule of '{conceptKey}.{slot}' accepts '{target}', which is not a concept of this language; leave the target empty to accept every named node.");
            }

            if (!byConcept.TryGetValue(conceptKey, out var slots))
            {
                slots = new Dictionary<string, ScopeBinding>(StringComparer.Ordinal);
                byConcept[conceptKey] = slots;
            }

            if (!slots.TryAdd(slot, new ScopeBinding(place, target)))
            {
                throw new ConceptDefinitionException(
                    $"Two scope rules answer for '{conceptKey}.{slot}'; a reference has one scope, so state it once.");
            }
        }

        var registry = ScopeProviderRegistry.Empty;

        foreach (var (conceptKey, slots) in byConcept)
        {
            registry = registry.Add(conceptKey, new NamedScopeProvider(slots));
        }

        return registry;
    }

    private static ScopePlace PlaceOf(string symbol) => symbol switch
    {
        ScopePlaces.Local => ScopePlace.Local,
        ScopePlaces.Ancestors => ScopePlace.Ancestors,
        ScopePlaces.Model => ScopePlace.Model,
        ScopePlaces.Project => ScopePlace.Project,
        ScopePlaces.Everywhere => ScopePlace.Everywhere,
        var other => throw new ConceptDefinitionException(
            $"'{other}' is not a place; a scope rule looks at {ScopePlaces.Local}, {ScopePlaces.Ancestors}, "
            + $"{ScopePlaces.Model}, {ScopePlaces.Project} or {ScopePlaces.Everywhere}."),
    };

    private static string Required(Node rule, string slot, string message) =>
        Optional(rule, slot) ?? throw new ConceptDefinitionException(message);

    private static string? Optional(Node rule, string slot) =>
        rule.GetOrNull(slot) is { Length: > 0 } value ? value : null;
}
