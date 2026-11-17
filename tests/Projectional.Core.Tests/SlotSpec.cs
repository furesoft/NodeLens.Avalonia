namespace Projectional.Core.Tests;

/// <summary>One slot a concept declares, described before the concept that will hold it exists.</summary>
/// <param name="ConceptKey">Which kind of slot it is: a property, a child or a reference declaration.</param>
/// <param name="Name">The slot's name, which is what the concept declares it under.</param>
/// <param name="TypeName">The declared type, for a property.</param>
/// <param name="DefaultValue">The value the property starts with, if any.</param>
/// <param name="Description">The sentence a hover shows, if any.</param>
/// <param name="ChildConceptKey">The concept a child slot holds.</param>
/// <param name="Cardinality">Whether a child slot holds one child or many.</param>
/// <param name="TargetConceptKey">The concept a reference slot points at.</param>
/// <param name="AllowOwnSubtree">Whether a reference may point into its own node's subtree.</param>
/// <param name="ShowDescriptionWhenFilled">Whether a hover shows the sentence over a value as well.</param>
/// <remarks>
/// A description rather than a node, because a slot is named in the call that also creates its concept -
/// <c>Concept("Rules.Rule", slots: [Property(...)])</c> evaluates the list first - while the node has to exist in the
/// concept's document. The builder therefore materialises these once the concept's document exists, which is what
/// keeps one definition per document.
/// </remarks>
internal sealed record SlotSpec(
    string ConceptKey,
    string Name,
    string? TypeName = null,
    string? DefaultValue = null,
    string? Description = null,
    string? ChildConceptKey = null,
    string? Cardinality = null,
    string? TargetConceptKey = null,
    bool AllowOwnSubtree = false,
    bool ShowDescriptionWhenFilled = false);
