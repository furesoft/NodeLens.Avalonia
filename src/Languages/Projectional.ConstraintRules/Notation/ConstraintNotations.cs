using Projectional.Core;
using Projectional.Language;

namespace Projectional.ConstraintRules;

/// <summary>
/// How a declared rule is drawn: a header saying which concept it is for, then the condition it fires on and the
/// finding it reports.
/// </summary>
/// <remarks>
/// The shape is one line per question, indented under the header, so a rule reads as the sentence a reviewer would
/// say out loud: "for this concept, when this is true, then this is wrong". A rule used to take four lines -
/// <c>constraint on</c>, <c>check</c>, <c>report</c> and <c>saying</c> - which split one finding across two of them
/// and left the condition apart from the word that makes it one.
/// <para>
/// The header's concept cell carries the empty text <c>every node</c>, which is the one place in the framework where
/// an empty property is the interesting state rather than the unfinished one - a rule about everything is a rule,
/// and the placeholder is what says so. <c>&lt;&lt; ... &gt;&gt;</c> is the empty-section placeholder the other
/// definition languages use. The message is quoted because it is prose and can hold anything, including the commas
/// and brackets that would otherwise read as part of the drawing; the code follows it because it is the finding's
/// identity rather than its content.
/// </remarks>
public static class ConstraintNotations
{
    private const string NoCheck = "<< what it checks >>";
    private const string NoCode = "<<no code>>";
    private const string NoMessage = "<< what is wrong >>";
    private const string EveryNode = "every node";

    /// <summary>The notation registry of the constraint language.</summary>
    public static NotationRegistry Registry { get; } = Create(ConstraintLanguage.Concepts);

    /// <summary>The notation registry of <paramref name="concepts"/>.</summary>
    /// <param name="concepts">The concepts to register notations against.</param>
    public static NotationRegistry Create(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        AddTo(notations);

        return notations;
    }

    /// <summary>Registers the notations into <paramref name="notations"/>.</summary>
    /// <param name="notations">The registry to add to.</param>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(ConstraintConceptKeys.Rule, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("rule", TextEmphasis.Bold),
                ed.Space,
                ed.Keyword("for"),
                ed.Space,
                ed.Property(ConstraintLanguage.ConceptKeySlot, emptyText: EveryNode)),
            ed.Indent(ed.Sequence(
                ed.Line(
                    ed.Keyword("when"),
                    ed.Space,
                    ed.ChildInline(ConstraintLanguage.BodySlot, NoCheck)),
                ed.Line(
                    ed.Keyword("then"),
                    ed.Space,
                    ed.Property(ConstraintLanguage.SeveritySlot),
                    ed.Space,
                    ed.Keyword("\""),
                    ed.Property(ConstraintLanguage.MessageSlot, emptyText: NoMessage),
                    ed.Keyword("\""),
                    ed.Space,
                    ed.Keyword("as"),
                    ed.Space,
                    ed.Property(ConstraintLanguage.CodeSlot, emptyText: NoCode))))));

        notations.Add(ConstraintConceptKeys.EmptySlot, ed => ed.Line(
            ed.Keyword("the slot"),
            ed.Space,
            ed.Property(ConstraintLanguage.TargetSlot),
            ed.Space,
            ed.Keyword("is empty")));

        notations.Add(ConstraintConceptKeys.DefaultValue, ed => ed.Line(
            ed.Keyword("the value of"),
            ed.Space,
            ed.Property(ConstraintLanguage.TargetSlot),
            ed.Space,
            ed.Keyword("is still the default")));

        notations.Add(ConstraintConceptKeys.SurroundingWhitespace, ed => ed.Line(
            ed.Keyword("the value of"),
            ed.Space,
            ed.Property(ConstraintLanguage.TargetSlot),
            ed.Space,
            ed.Keyword("has whitespace around it")));

        notations.Add(ConstraintConceptKeys.EmptyReference, ed => ed.Line(
            ed.Keyword("the reference"),
            ed.Space,
            ed.Property(ConstraintLanguage.TargetSlot),
            ed.Space,
            ed.Keyword("points at nothing")));
    }
}
