using Projectional.Language;

namespace Projectional.EvaluationRules;

/// <summary>
/// The notation of every concept <see cref="EvaluationRuleLanguage"/> declares.
/// </summary>
/// <remarks>
/// Each body reads as the sentence it is: <c>property value</c>, <c>child inner</c>, <c>operator operator over left and
/// right</c>, <c>choose by condition then ... otherwise ...</c>. A rule is a document of its own, so its body may stand on
/// its own line - which is what lets the choose body draw its two arms indented under it.
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

        // The right operand is named by a *property*, not held as a child, so it is drawn as one: a rule that leaves it
        // empty reads as `... over operand` and the empty cell says what is missing, which is the same bargain an optional
        // operand elsewhere gets. Conditional cells cannot do it here - they ask the slot whether it is filled, and this
        // one names a slot rather than being one.
        notations.Add(EvaluationRuleConceptKeys.Operator, ed => ed.Line(
            ed.Keyword("operator"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.OperatorSlot),
            ed.Space,
            ed.Keyword("over"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.LeftSlot),
            ed.Space,
            ed.Keyword("and"),
            ed.Space,
            ed.Property(EvaluationRuleLanguage.RightSlot, emptyText: "<<unary>>")));

        notations.Add(EvaluationRuleConceptKeys.Choose, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("choose by"),
                ed.Space,
                ed.Property(EvaluationRuleLanguage.ConditionSlot)),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("then"), ed.Space, ed.ChildInline(EvaluationRuleLanguage.ThenSlot)),
                ed.Line(ed.Keyword("otherwise"), ed.Space, ed.ChildInline(EvaluationRuleLanguage.OtherwiseSlot))))));
    }
}
