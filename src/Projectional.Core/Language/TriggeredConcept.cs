namespace Projectional.Core;

/// <summary>A concept a trigger would create, together with the declaration that says so.</summary>
/// <remarks>
/// A pair rather than the trigger alone, because the trigger does not name its concept - it is declared on it. The
/// registry turns a declaration into this pair when the editor asks "what could be created here", so the caller
/// gets the concept it needs to check slots and to wrap without a second lookup.
/// </remarks>
/// <param name="Concept">The concept that would be created.</param>
/// <param name="Trigger">The declaration on it that claims the keystroke.</param>
public readonly record struct TriggeredConcept(Concept Concept, ConceptTrigger Trigger)
{
    public override string ToString() => $"{Concept.Key} <- {Trigger.AnchorConcept}";
}
