using Projectional.Core;
using Projectional.Language;

namespace Projectional.TypeRules;

/// <summary>
/// How a type rule is drawn, in the shape MPS writes a typesystem: a header per rule, then the body
/// indented under it.
/// </summary>
/// <remarks>
/// The shape is worth copying because it is the shape the problem has: a reader sees which concept the
/// rule is about in the header and what it answers underneath, so a rule can be reviewed without running
/// it. <c>&lt;&lt; ... &gt;&gt;</c> is the empty-section placeholder, the same glyph the structure and
/// editor languages use - the same state gets the same glyph.
/// </remarks>
public static class TypeRuleNotations
{
    private const string NoEntries = "<< ... >>";

    /// <summary>The notation registry of the type-rule language.</summary>
    public static NotationRegistry Registry { get; } = Create(TypeRuleLanguage.Concepts);

    /// <summary>The notation registry of <paramref name="concepts"/>.</summary>
    public static NotationRegistry Create(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        AddTo(notations);

        return notations;
    }

    /// <summary>Registers the notations into <paramref name="notations"/>.</summary>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(TypeRuleConceptKeys.Rule, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("rule", TextEmphasis.Bold),
                ed.Space,
                ed.Property(TypeRuleLanguage.ConceptKeySlot),
                ed.Space,
                ed.Keyword("is")),
            ed.Indent(ed.Line(ed.ChildInline(TypeRuleLanguage.BodySlot)))));

        notations.Add(TypeRuleConceptKeys.Fixed, ed => ed.Line(
            ed.Keyword("type"),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.TypeSlot)));

        notations.Add(TypeRuleConceptKeys.Child, ed => ed.Line(
            ed.Keyword("type of"),
            ed.Space,
            ed.Property(TypeRuleLanguage.SlotSlot)));

        notations.Add(TypeRuleConceptKeys.Switch, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("switch on"),
                ed.Space,
                ed.Property(TypeRuleLanguage.PropertySlot)),
            ed.Indent(ed.Sequence(
                ed.Children(TypeRuleLanguage.CaseSlot, NoEntries),
                ed.Line(ed.Keyword("otherwise:")),
                ed.Indent(ed.Line(ed.ChildInline(TypeRuleLanguage.OtherwiseSlot)))))));

        notations.Add(TypeRuleConceptKeys.Case, ed => ed.Line(
            ed.Keyword("when"),
            ed.Space,
            ed.Property(TypeRuleLanguage.ValueSlot),
            ed.Space,
            ed.Keyword("then"),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.BodySlot)));

        // The type expressions all draw exactly what IType.Name prints for what they lower to, so the
        // source of a rule and the sentence a finding quotes about it read the same.
        notations.Add(TypeRuleConceptKeys.Primitive, ed => ed.Line(
            ed.Property(TypeRuleLanguage.NameSlot)));

        notations.Add(TypeRuleConceptKeys.Record, ed => ed.Line(
            ed.Keyword("("),
            ed.InlineCollection(TypeRuleLanguage.FieldsSlot, ed.Punct(","), NoEntries),
            ed.Keyword(")")));

        notations.Add(TypeRuleConceptKeys.Field, ed => ed.Line(
            ed.Property(TypeRuleLanguage.NameSlot),
            ed.Punct(":"),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.TypeSlot)));

        notations.Add(TypeRuleConceptKeys.List, ed => ed.Line(
            ed.Keyword("["),
            ed.ChildInline(TypeRuleLanguage.ElementSlot),
            ed.Keyword("]")));

        notations.Add(TypeRuleConceptKeys.Optional, ed => ed.Line(
            ed.ChildInline(TypeRuleLanguage.InnerSlot),
            ed.Keyword("?")));

        // A join and a meet draw as the operators JoinType.Name and MeetType.Name print, with no spaces, so the
        // source and the sentence a finding quotes are the same text.
        notations.Add(TypeRuleConceptKeys.Join, ed => ed.Line(
            ed.ChildInline(TypeRuleLanguage.LeftSlot),
            ed.Keyword("|"),
            ed.ChildInline(TypeRuleLanguage.RightSlot)));

        notations.Add(TypeRuleConceptKeys.Meet, ed => ed.Line(
            ed.ChildInline(TypeRuleLanguage.LeftSlot),
            ed.Keyword("&"),
            ed.ChildInline(TypeRuleLanguage.RightSlot)));

        // A name draws as itself; what it expands to is the model's business, not the reader's.
        notations.Add(TypeRuleConceptKeys.Ref, ed => ed.Line(
            ed.Property(TypeRuleLanguage.NameSlot)));

        // The same "type" keyword a fixed body starts with, so one reading covers both: `type int` names the
        // framework's type, `type Amount = int` names the language's.
        notations.Add(TypeRuleConceptKeys.Named, ed => ed.Line(
            ed.Keyword("type"),
            ed.Space,
            ed.Property(TypeRuleLanguage.NameSlot),
            ed.Space,
            ed.Keyword("="),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.TypeSlot)));

        // A variable draws as the `?` spelling IType.Name prints for it, so an equation in the source and the
        // sentence a failed solve quotes read the same.
        notations.Add(TypeRuleConceptKeys.TypeVariable, ed => ed.Line(
            ed.Keyword("?"),
            ed.Property(TypeRuleLanguage.NameSlot)));

        notations.Add(TypeRuleConceptKeys.Equations, ed => ed.Sequence(
            ed.Children(TypeRuleLanguage.EquationsSlot, NoEntries)));

        notations.Add(TypeRuleConceptKeys.Equation, ed => ed.Line(
            ed.ChildInline(TypeRuleLanguage.LeftSlot),
            ed.Space,
            ed.Property(TypeRuleLanguage.RelationSlot),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.RightSlot)));

        // A complaint draws as the sentence a finding quotes, so the author reads the diagnostic before it is
        // reported: the severity first, then the message the user will see.
        notations.Add(TypeRuleConceptKeys.Complain, ed => ed.Line(
            ed.Keyword("report"),
            ed.Space,
            ed.Property(TypeRuleLanguage.SeveritySlot),
            ed.Space,
            ed.Keyword("\""),
            ed.Property(TypeRuleLanguage.MessageSlot),
            ed.Keyword("\"")));
    }
}
