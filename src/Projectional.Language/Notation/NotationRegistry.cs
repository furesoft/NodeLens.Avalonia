using System.Diagnostics.CodeAnalysis;
using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// The notation of every concept in a language, validated once against the concept registry so that
/// the projection engine can assume a notation only ever names slots that exist.
/// </summary>
/// <remarks>
/// A concept may be given several notations, one per <see cref="Notation.Variant"/>. The registry
/// keeps them all and answers for a <see cref="INotationChoice"/>; it does not decide which one a
/// reader should see, because that is view state and this is a property of the language.
/// </remarks>
public sealed class NotationRegistry
{
    private readonly ConceptRegistry _concepts;
    private readonly Dictionary<string, List<Notation>> _byConceptKey = new(StringComparer.Ordinal);
    private readonly List<Notation> _ordered = [];

    public NotationRegistry(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);
        _concepts = concepts;
    }

    /// <summary>Notations in registration order, every variant included.</summary>
    public IReadOnlyList<Notation> Notations => _ordered;

    /// <summary>Declares how <paramref name="conceptKey"/> is drawn by default.</summary>
    /// <exception cref="NotationException">The notation names something the concept does not have.</exception>
    public Notation Add(string conceptKey, Func<NotationEditor, BlockNotationCell> build) =>
        Add(conceptKey, Notation.DefaultVariant, build);

    /// <summary>Declares how <paramref name="conceptKey"/> is drawn in <paramref name="variant"/>.</summary>
    /// <exception cref="NotationException">
    /// The notation names something the concept does not have, or the variant is already declared.
    /// </exception>
    public Notation Add(string conceptKey, string variant, Func<NotationEditor, BlockNotationCell> build)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentException.ThrowIfNullOrEmpty(variant);
        ArgumentNullException.ThrowIfNull(build);

        var root = build(NotationEditor.Create())
            ?? throw new NotationException(
                $"The '{variant}' notation of concept '{conceptKey}' built no root cell.");

        return Add(new Notation(conceptKey, variant, root));
    }

    /// <summary>Declares a notation that has already been built.</summary>
    /// <exception cref="NotationException">The notation names something the concept does not have.</exception>
    public Notation Add(Notation notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        if (!_concepts.TryGet(notation.ConceptKey, out var concept))
        {
            throw new NotationException(
                $"Concept '{notation.ConceptKey}' is given a notation, but no such concept is declared.");
        }

        if (!_byConceptKey.TryGetValue(notation.ConceptKey, out var variants))
        {
            variants = [];
            _byConceptKey.Add(notation.ConceptKey, variants);
        }

        // One notation per (concept, variant) is the rule that makes a choice well defined: two
        // notations claiming the same name would make "which one did I ask for" unanswerable.
        if (variants.Exists(existing => string.Equals(existing.Variant, notation.Variant, StringComparison.Ordinal)))
        {
            throw new NotationException(
                $"Concept '{notation.ConceptKey}' already has a '{notation.Variant}' notation.");
        }

        foreach (var cell in notation.SelfAndDescendants())
        {
            ValidateCell(concept, cell);
        }

        variants.Add(notation);
        _ordered.Add(notation);
        return notation;
    }

    /// <summary>The default notation of <paramref name="conceptKey"/>, when it declares one.</summary>
    public bool TryGet(string? conceptKey, [NotNullWhen(true)] out Notation? notation) =>
        TryGet(conceptKey, Notation.DefaultVariant, out notation);

    /// <summary>The notation of <paramref name="conceptKey"/> in <paramref name="variant"/>, when it declares one.</summary>
    public bool TryGet(string? conceptKey, string? variant, [NotNullWhen(true)] out Notation? notation)
    {
        notation = conceptKey is not null
            && variant is not null
            && _byConceptKey.TryGetValue(conceptKey, out var variants)
                ? variants.Find(existing => string.Equals(existing.Variant, variant, StringComparison.Ordinal))
                : null;

        return notation is not null;
    }

    /// <summary>The default notation of <paramref name="conceptKey"/>.</summary>
    /// <exception cref="KeyNotFoundException">The concept declares no default notation.</exception>
    public Notation Get(string conceptKey) => Get(conceptKey, Notation.DefaultVariant);

    /// <summary>The notation of <paramref name="conceptKey"/> in <paramref name="variant"/>.</summary>
    /// <exception cref="KeyNotFoundException">The concept declares no such variant.</exception>
    public Notation Get(string conceptKey, string variant) => TryGet(conceptKey, variant, out var notation)
        ? notation
        : throw new KeyNotFoundException(
            $"No '{variant}' notation is declared for concept '{conceptKey}'.");

    /// <summary>
    /// The variants declared for <paramref name="conceptKey"/>, in registration order; empty when the
    /// concept has no notation of its own.
    /// </summary>
    public IReadOnlyList<string> VariantsOf(string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(conceptKey);

        return _byConceptKey.TryGetValue(conceptKey, out var variants)
            ? [.. variants.Select(notation => notation.Variant)]
            : [];
    }

    /// <summary>
    /// The notation that applies to <paramref name="concept"/> when the reader expresses no opinion:
    /// its own default, or the nearest one it inherits.
    /// </summary>
    public Notation? FindFor(Concept concept) => FindFor(concept, null);

    /// <summary>The notation that applies to <paramref name="concept"/> for <paramref name="choice"/>.</summary>
    /// <remarks>
    /// <para>
    /// The requested variant is searched up the super-concept chain first and only then the default,
    /// which is what lets an abstract super concept be the only one that knows how to be a table: a
    /// concept-specific default must not shadow the variant the reader asked for, or the request
    /// would silently produce text.
    /// </para>
    /// <para>
    /// Super concepts are searched in declaration order, which makes the outcome predictable when a
    /// concept has more than one. When a concept declares notations but no default at all, its first
    /// declared variant is used rather than nothing - a language that only ever wants to be a table
    /// should not have to declare a text form it never renders.
    /// </para>
    /// </remarks>
    public Notation? FindFor(Concept concept, INotationChoice? choice)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (choice?.VariantOf(concept.Key) is { Length: > 0 } wanted
            && FindVariant(concept, wanted) is { } chosen)
        {
            return chosen;
        }

        return FindVariant(concept, Notation.DefaultVariant) ?? FindAny(concept);
    }

    private Notation? FindVariant(Concept concept, string variant)
    {
        if (TryGet(concept.Key, variant, out var own))
        {
            return own;
        }

        foreach (var super in concept.SuperConcepts)
        {
            if (FindVariant(super, variant) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    private Notation? FindAny(Concept concept)
    {
        if (_byConceptKey.TryGetValue(concept.Key, out var own))
        {
            return own[0];
        }

        foreach (var super in concept.SuperConcepts)
        {
            if (FindAny(super) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    private void ValidateCell(Concept concept, NotationCell cell)
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

            case InlineCollectionNotationCell inlineCollection:
                RequireChildSlotExists(concept, inlineCollection.Slot);
                break;

            case TableNotationCell table:
                ValidateTable(concept, table);
                break;

            case ConditionalNotationCell conditional:
                ValidateConditional(concept, conditional);
                break;

            case WhenFilledNotationCell whenFilled:
                RequireChildSlotExists(concept, whenFilled.Slot);
                break;
        }
    }

    /// <summary>
    /// Checks a condition against the property it reads. A value the type would refuse can never hold, so the
    /// conditional would never draw - a mistake that is invisible at run time and cheap to refuse here.
    /// </summary>
    private static void ValidateConditional(Concept concept, ConditionalNotationCell conditional)
    {
        var property = concept.FindProperty(conditional.Slot)
            ?? throw new NotationException(
                $"The notation of '{concept.Key}' draws cells when '{conditional.Slot}' holds a value, but the concept declares no such property.");

        if (!property.Type.IsValid(conditional.Value))
        {
            throw new NotationException(
                $"The notation of '{concept.Key}' draws cells when '{conditional.Slot}' is \"{conditional.Value}\", which is not a value of '{property.Type.Name}'.");
        }
    }

    /// <summary>
    /// Checks a table against the concept its rows are drawn from, which the cell itself cannot do
    /// because it names slots of a <em>different</em> concept.
    /// </summary>
    private void ValidateTable(Concept concept, TableNotationCell table)
    {
        var rows = concept.FindChild(table.Slot)
            ?? throw new NotationException(
                $"The notation of '{concept.Key}' draws a table over '{table.Slot}', but the concept declares no such child.");

        if (!_concepts.TryGet(rows.ChildConceptKey, out var rowConcept))
        {
            throw new NotationException(
                $"The notation of '{concept.Key}' draws a table over '{table.Slot}', whose children are '{rows.ChildConceptKey}', but no such concept is declared.");
        }

        foreach (var column in table.Columns)
        {
            if (rowConcept.FindProperty(column.Slot) is not null)
            {
                continue;
            }

            if (rowConcept.FindChild(column.Slot) is { } child)
            {
                // A cell holds one child, so a list column would silently drop every row item after
                // the first - the same reason an inline child cell refuses a many-valued slot.
                if (child.Cardinality != Cardinality.One)
                {
                    throw new NotationException(
                        $"The table of '{concept.Key}' shows column '{column.Slot}', but '{rowConcept.Key}' declares that child as many-valued; a cell can only show one child.");
                }

                continue;
            }

            // A reference is one slot with one value, so a column can show it exactly as a property can.
            // That is what a foreign key needs: a table of rows whose column names another row.
            if (rowConcept.FindReference(column.Slot) is not null)
            {
                continue;
            }

            throw new NotationException(
                $"The table of '{concept.Key}' shows column '{column.Slot}', but '{rowConcept.Key}' declares no property, child or reference of that name.");
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
