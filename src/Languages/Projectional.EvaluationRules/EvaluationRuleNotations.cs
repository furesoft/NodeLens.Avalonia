using Projectional.Language;

namespace Projectional.EvaluationRules;

/// <summary>
/// The notation of every concept <see cref="EvaluationRuleLanguage"/> declares.
/// </summary>
/// <remarks>
/// Each body reads as the sentence it is: <c>property value</c>, <c>operator operator over ... and ...</c>,
/// <c>choose ... then ... otherwise ...</c>, <c>first entry of entries where ... answers ...</c>. A rule is a document of its
/// own, so a body whose answer is two arms may stand on several lines - which is what makes a rule readable as the
/// decision it describes.
/// </remarks>
public static class EvaluationRuleNotations
{
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(EvaluationRuleConceptKeys.Rule, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("rule for"),
                ed.Space,
                ed.Property(EvaluationRuleLanguage.ConceptKeySlot, emptyText: "<<concept>>")),
            ed.Indent(ed.Line(ed.ChildInline(EvaluationRuleLanguage.BodySlot)))));

        notations.Add(EvaluationRuleConceptKeys.Property, ed => ed.Line(
            ed.Keyword("property"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.SlotSlot)));

        notations.Add(EvaluationRuleConceptKeys.Child, ed => ed.Line(
            ed.Keyword("child"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.SlotSlot)));

        notations.Add(EvaluationRuleConceptKeys.Variable, ed => ed.Line(
            ed.Keyword("variable"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.SlotSlot)));

        notations.Add(EvaluationRuleConceptKeys.Constant, ed => ed.Line(
            ed.Property("kind", emptyText: "<<kind>>"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.ValueSlot, emptyText: "<<value>>")));

        // The right operand is a body and may be absent, so it is drawn as a conditional block over a slot that can hold
        // nothing: `operator + over ...` is the unary reading, and the `and` only appears when there is a second operand.
        // The symbol the body applies, or - left empty - the node's own: the empty cell says where it comes from, which is
        // the one thing a reader cannot infer from `operator ... over ...`.
        notations.Add(EvaluationRuleConceptKeys.Operator, ed => ed.Line(
            ed.Keyword("operator"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.SymbolSlot, emptyText: "<<symbol>>"),
            ed.Space,
            ed.Keyword("over"),
            ed.Space,
            ed.ChildInline(EvaluationRuleLanguage.LeftSlot),
            ed.WhenFilled(
                EvaluationRuleLanguage.RightSlot,
                ed.Space,
                ed.Keyword("and"),
                ed.Space,
                ed.ChildInline(EvaluationRuleLanguage.RightSlot))));

        notations.Add(EvaluationRuleConceptKeys.Choose, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("choose"),
                ed.Space,
                ed.ChildInline(EvaluationRuleLanguage.ConditionSlot)),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("then"), ed.Space, ed.ChildInline(EvaluationRuleLanguage.ThenSlot)),
                ed.Line(ed.Keyword("otherwise"), ed.Space, ed.ChildInline(EvaluationRuleLanguage.OtherwiseSlot))))));

        // The two bodies read as one sentence, because that is what they say: the entries are tried in order, and the
        // answer comes from the entry that applied.
        notations.Add(EvaluationRuleConceptKeys.First, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("first of"),
                ed.Space,
                ed.Property(EvaluationRuleLanguage.OverSlot, emptyText: "<<slot>>"),
                ed.Space,
                ed.Keyword("where"),
                ed.Space,
                ed.ChildInline(EvaluationRuleLanguage.WhenSlot),
                ed.Space,
                ed.Keyword("answers"),
                ed.Space,
                ed.ChildInline(EvaluationRuleLanguage.ThenSlot)),
            ed.WhenFilledBlock(
                EvaluationRuleLanguage.OtherwiseSlot,
                ed.Indent(ed.Line(
                    ed.Keyword("otherwise"),
                    ed.Space,
                    ed.ChildInline(EvaluationRuleLanguage.OtherwiseSlot))))));

        notations.Add(EvaluationRuleConceptKeys.Bind, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("bind"),
                ed.Space,
                ed.Property(EvaluationRuleLanguage.NameSlot, emptyText: "<<name>>"),
                ed.Space,
                ed.Keyword("to"),
                ed.Space,
                ed.ChildInline(EvaluationRuleLanguage.ValueSlot),
                ed.Space,
                ed.Keyword("then answer")),
            ed.Indent(ed.Line(ed.ChildInline(EvaluationRuleLanguage.BodySlot)))));

        notations.Add(EvaluationRuleConceptKeys.Fail, ed => ed.Line(
            ed.Keyword("refuse"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.MessageSlot, emptyText: "<<message>>")));
    }
}
