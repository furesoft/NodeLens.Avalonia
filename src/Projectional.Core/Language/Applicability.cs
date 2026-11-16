using System.Collections.Immutable;

namespace Projectional.Core;

/// <summary>Where a concept's nodes may stand - the "conditional concept" a language declares.</summary>
/// <remarks>
/// <para>
/// A child slot already says which <em>kind</em> of thing it holds (<see cref="ChildSlot.ChildConceptKey"/>),
/// but that is a statement about the slot, and it is the same wherever the slot appears. Some concepts are not
/// allowed everywhere their type is: a C# attribute may stand on a class, a method or a parameter and nowhere
/// else, and an XML comment only in front of a class or method definition. Where such a concept must not stand,
/// the editor has to stop offering it - a completion list that proposes something the language forbids teaches
/// the user a mistake.
/// </para>
/// <para>
/// So the constraint lives on the concept, as a list of places, and it is consulted in three places and
/// enforced in one:
/// </para>
/// <list type="bullet">
/// <item><description>completion filters its proposals by it;</description></item>
/// <item><description>filling an empty slot with Enter skips a concept that may not stand there;</description></item>
/// <item><description>typing into an empty slot skips one too;</description></item>
/// <item><description>and the model refuses the placement outright, so no action, no load and no replay can
/// produce a node in a place the language does not allow.</description></item>
/// </list>
/// <para>
/// It is deliberately <b>not</b> <see cref="ConceptDeclaration.IsOfferedInCompletion"/>. That flag silences the
/// list and nothing else, because hiding a literal from a menu must not make it impossible to create. This one
/// is about legality, so it has to hold everywhere - the two must not be merged.
/// </para>
/// </remarks>
public sealed class Applicability
{
    private readonly ImmutableArray<ConceptPlace> _places;

    private Applicability(ImmutableArray<ConceptPlace> places) => _places = places;

    /// <summary>Every place its type is accepted in, which is what a concept means until it says otherwise.</summary>
    public static Applicability Anywhere { get; } = new(ImmutableArray<ConceptPlace>.Empty);

    /// <summary>Only the given places, and no others.</summary>
    /// <param name="places">Where nodes of this concept may stand.</param>
    /// <exception cref="ArgumentException">No place was given.</exception>
    /// <remarks>
    /// An empty list would forbid the concept everywhere, which is a concept that cannot exist; a language that
    /// means that should not declare it at all. Refusing is what keeps "declared nothing" and "declared
    /// nowhere" from being one state - <see cref="Anywhere"/> is the absent case.
    /// </remarks>
    public static Applicability Only(params ConceptPlace[] places)
    {
        ArgumentNullException.ThrowIfNull(places);

        if (places.Length == 0)
        {
            throw new ArgumentException(
                "A concept that may stand nowhere cannot exist; use Applicability.Anywhere instead.",
                nameof(places));
        }

        return new Applicability([.. places]);
    }

    /// <summary>Where nodes of this concept may stand, in declaration order.</summary>
    public IReadOnlyList<ConceptPlace> Places => _places;

    /// <summary>Whether the concept may stand anywhere its type is accepted.</summary>
    public bool IsAnywhere => _places.IsDefaultOrEmpty;

    /// <summary>Whether a node of this concept may stand in <paramref name="slot"/> of <paramref name="parent"/>.</summary>
    /// <param name="parent">The node that would hold it, or <see langword="null"/> for a model root.</param>
    /// <param name="slot">That node's child slot, or <see langword="null"/> for a model root.</param>
    /// <remarks>
    /// A concept that names places can never be a model root: a root has no parent and no slot, so none of its
    /// places could match. That follows from the same rule rather than being a special case.
    /// </remarks>
    public bool Allows(Concept? parent, string? slot) =>
        IsAnywhere || (parent is not null && _places.Any(place => place.Allows(parent, slot)));

    public override string ToString() =>
        IsAnywhere ? "anywhere" : string.Join(", ", _places.Select(place => place.ToString()));
}
