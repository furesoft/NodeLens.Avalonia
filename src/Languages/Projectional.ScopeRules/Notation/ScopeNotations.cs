using Projectional.Language;

namespace Projectional.ScopeRules.Notation;

/// <summary>How a scope rule is drawn: one line that reads like the sentence it is.</summary>
/// <remarks>
/// The words are the order of the questions a rule answers - whose reference, which names count and where they are
/// written - so a reader can check a rule without opening the language it belongs to. An empty target is a decision
/// ("every named node") rather than a value the author forgot, which is what its <c>*</c> stand-in says.
/// </remarks>
public static class ScopeNotations
{
    /// <summary>Registers the scope language's own notation.</summary>
    /// <param name="notations">The registry to add to, which must know the scope language's concepts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(ScopeConceptKeys.Rule, ed => ed.Line(
            ed.Keyword("scope"),
            ed.Space,
            ed.Property(ScopeLanguage.ConceptKeySlot, emptyText: "<concept>"),
            ed.Punct("."),
            ed.Property(ScopeLanguage.SlotSlot, emptyText: "<slot>"),
            ed.Space,
            ed.Keyword("sees"),
            ed.Space,
            ed.Property(ScopeLanguage.TargetSlot, emptyText: "*"),
            ed.Space,
            ed.Keyword("in"),
            ed.Space,
            ed.Property(ScopeLanguage.PlaceSlot)));
    }
}
