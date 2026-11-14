using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// The notation of every concept in a language, validated once against the concept registry so that
/// the projection engine can assume a notation only ever names slots that exist.
/// </summary>
public sealed class NotationRegistry
{
    private readonly ConceptRegistry _concepts;
    private readonly Dictionary<string, Notation> _byConceptKey = new(StringComparer.Ordinal);
    private readonly List<Notation> _ordered = [];

    public NotationRegistry(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);
        _concepts = concepts;
    }

    /// <summary>Notations in registration order.</summary>
    public IReadOnlyList<Notation> Notations => _ordered;

    /// <summary>Declares how <paramref name="conceptKey"/> is drawn.</summary>
    /// <exception cref="NotationException">The notation names something the concept does not have.</exception>
    public Notation Add(string conceptKey, Func<NotationEditor, BlockNotationCell> build)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(build);

        var root = build(NotationEditor.Create())
            ?? throw new NotationException($"The notation of concept '{conceptKey}' built no root cell.");

        return Add(new Notation(conceptKey, root));
    }

    /// <summary>Declares a notation that has already been built.</summary>
    /// <exception cref="NotationException">The notation names something the concept does not have.</exception>
    public Notation Add(Notation notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        if (_byConceptKey.ContainsKey(notation.ConceptKey))
        {
            throw new NotationException($"Concept '{notation.ConceptKey}' is given a notation more than once.");
        }

        if (!_concepts.TryGet(notation.ConceptKey, out var concept))
        {
            throw new NotationException(
                $"Concept '{notation.ConceptKey}' is given a notation, but no such concept is declared.");
        }

        foreach (var cell in notation.SelfAndDescendants())
        {
            ValidateCell(concept, cell);
        }

        _byConceptKey.Add(notation.ConceptKey, notation);
        _ordered.Add(notation);
        return notation;
    }

    public bool TryGet(string? conceptKey, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Notation? notation)
    {
        notation = conceptKey is not null && _byConceptKey.TryGetValue(conceptKey, out var found) ? found : null;
        return notation is not null;
    }

    public Notation Get(string conceptKey) => TryGet(conceptKey, out var notation)
        ? notation
        : throw new KeyNotFoundException($"No notation is declared for concept '{conceptKey}'.");

    /// <summary>
    /// The notation that applies to <paramref name="concept"/>: its own, or the nearest one it
    /// inherits. Super concepts are searched in declaration order, which makes the outcome
    /// predictable when a concept has more than one.
    /// </summary>
    public Notation? FindFor(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (TryGet(concept.Key, out var own))
        {
            return own;
        }

        foreach (var super in concept.SuperConcepts)
        {
            if (FindFor(super) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    private static void ValidateCell(Concept concept, NotationCell cell)
    {
        switch (cell)
        {
            case PropertyNotationCell property:
                if (concept.FindProperty(property.Slot) is null)
                {
                    throw new NotationException(
                        $"The notation of '{concept.Key}' shows property '{property.Slot}', but the concept declares no such property.");
                }

                break;

            case ReferenceNotationCell reference:
                if (concept.FindReference(reference.Slot) is null)
                {
                    throw new NotationException(
                        $"The notation of '{concept.Key}' shows reference '{reference.Slot}', but the concept declares no such reference.");
                }

                break;

            case SingleChildNotationCell single:
                var childSlot = concept.FindChild(single.Slot)
                    ?? throw new NotationException(
                        $"The notation of '{concept.Key}' shows child '{single.Slot}', but the concept declares no such child.");

                if (childSlot.Cardinality != Cardinality.One)
                {
                    // Drawing only the first child of a list would silently hide the others.
                    throw new NotationException(
                        $"The notation of '{concept.Key}' draws child '{single.Slot}' inline, but the slot holds many children; use a child cell instead.");
                }

                break;

            case ChildNotationCell children:
                RequireChildSlotExists(concept, children.Slot);
                break;

            case CollectionNotationCell collection:
                RequireChildSlotExists(concept, collection.Slot);
                break;
        }
    }

    private static void RequireChildSlotExists(Concept concept, string slot)
    {
        if (concept.FindChild(slot) is null)
        {
            throw new NotationException(
                $"The notation of '{concept.Key}' draws children of '{slot}', but the concept declares no such child.");
        }
    }
}
