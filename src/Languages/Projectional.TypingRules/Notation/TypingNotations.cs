using Projectional.Language;

namespace Projectional.TypingRules.Notation;

/// <summary>How a typing rule is drawn: one line that reads like the sentence it is.</summary>
/// <remarks>
/// The words are the order of the questions a rule answers - which concept, how the text is recognised, the text it is
/// recognised by, and where it is written - so a reader can check a rule without opening the language it belongs to.
/// The text is drawn only for the two claims that read one, because <c>digits</c> and <c>name</c> have nothing to say
/// about a particular text and a placeholder there would read like a value the author forgot.
/// </remarks>
public static class TypingNotations
{
    /// <summary>Registers the typing language's own notation.</summary>
    /// <param name="notations">The registry to add to, which must know the typing language's concepts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(TypingConceptKeys.Rule, ed => ed.Line(
            ed.Keyword("typing"),
            ed.Space,
            ed.Property(TypingLanguage.ConceptKeySlot, emptyText: "<concept>"),
            ed.Space,
            ed.Keyword("claims"),
            ed.Space,
            ed.Property(TypingLanguage.ClaimsSlot),
            ed.Conditional(
                TypingLanguage.ClaimsSlot,
                TypingClaims.Prefix,
                ed.Space,
                ed.Property(TypingLanguage.TextSlot, emptyText: "<prefix>")),
            ed.Conditional(
                TypingLanguage.ClaimsSlot,
                TypingClaims.AnyOf,
                ed.Space,
                ed.Property(TypingLanguage.TextSlot, emptyText: "<words>")),
            ed.Space,
            ed.Keyword("writes"),
            ed.Space,
            ed.Property(TypingLanguage.PropertySlotSlot, emptyText: "<no property>")));
    }
}
