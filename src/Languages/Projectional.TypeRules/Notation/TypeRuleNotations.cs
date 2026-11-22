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

    /// <summary>What a replacement shows when it states no relations, which is a finished rule and not an empty one.</summary>
    private const string NothingToProve = "nothing";

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
            ed.Indent(ed.Indent(ed.Line(ed.ChildInline(TypeRuleLanguage.BodySlot))))));

        notations.Add(TypeRuleConceptKeys.Fixed, ed => ed.Line(
            ed.Keyword("type"),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.TypeSlot)));

        notations.Add(TypeRuleConceptKeys.Child, ed => ed.Line(
            ed.Keyword("type of"),
            ed.Space,
            ed.Property(TypeRuleLanguage.SlotSlot)));

        // "The widest of these two" reads as a question rather than as an operator, because it is one: the answer
        // depends on the language's edges, which a reader of the source cannot see.
        notations.Add(TypeRuleConceptKeys.Widest, ed => ed.Line(
            ed.Keyword("widest of"),
            ed.Space,
            ed.Property(TypeRuleLanguage.LeftSlot),
            ed.Space,
            ed.Keyword("and"),
            ed.Space,
            ed.Property(TypeRuleLanguage.RightSlot)));

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

        // A declared edge draws as the relation it states, with the `<:` spelling TypeEquation.ToString() prints,
        // so a subtyping declaration and the sentence a failed solve quotes read the same.
        notations.Add(TypeRuleConceptKeys.Supertypes, ed => ed.Line(
            ed.ChildInline(TypeRuleLanguage.SubtypeSlot),
            ed.Space,
            ed.Keyword("<:"),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.SupertypeSlot)));

        // An interchange is not a relation, so it has no symbol to draw: it reads as the sentence it means, which
        // is also the sentence a complaint about two incomparable operands is about.
        notations.Add(TypeRuleConceptKeys.Comparable, ed => ed.Line(
            ed.Keyword("comparable"),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.LeftSlot),
            ed.Space,
            ed.Keyword("with"),
            ed.Space,
            ed.ChildInline(TypeRuleLanguage.RightSlot)));

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

        // A replacement draws as the claim it rewrites - the two patterns with the relation's own symbol between
        // them, as an equation and a declared edge draw - followed by what it says instead. An empty `instead` is
        // not a slot waiting to be filled but the statement that the claim holds outright, so it says so.
        notations.Add(TypeRuleConceptKeys.Replacement, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("replace"),
                ed.Space,
                ed.ChildInline(TypeRuleLanguage.LeftSlot),
                ed.Space,
                ed.Property(TypeRuleLanguage.ClaimSlot),
                ed.Space,
                ed.ChildInline(TypeRuleLanguage.RightSlot),
                ed.Space,
                ed.Keyword("by")),
            ed.Indent(ed.Children(TypeRuleLanguage.InsteadSlot, NothingToProve))));

        // A complaint draws as the sentence a finding quotes, so the author reads the diagnostic before it is
        // reported: the severity on the report line, then the message the user will see on a line of its own. The
        // message is prose and often long, so keeping it inline with the severity and the code would push the
        // condition out of the window - and the condition is the part that says when the complaint fires.
        // The code follows the message and is drawn even when it is empty - a property cell is never hidden, because
        // the caret needs something to stand on (see guide-language.md), so an unset code shows the placeholder and
        // typing over it writes one. A condition draws only when it is there, so an unconditional complaint reads as
        // the sentence it always reports.
        notations.Add(TypeRuleConceptKeys.Complain, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("report"),
                ed.Space,
                ed.Property(TypeRuleLanguage.SeveritySlot)),
            ed.Indent(ed.Sequence(
                ed.Line(
                    ed.Keyword("\""),
                    ed.Property(TypeRuleLanguage.MessageSlot),
                    ed.Keyword("\"")),
                ed.Line(
                    ed.Keyword("code"),
                    ed.Space,
                    ed.Property(TypeRuleLanguage.CodeSlot, emptyText: "<none>"),
                    ed.WhenFilled(
                        TypeRuleLanguage.WhenSlot,
                        ed.Space,
                        ed.Keyword("when"),
                        ed.Space,
                        ed.ChildInline(TypeRuleLanguage.WhenSlot)))))));

        // A condition draws as the sentence it means: the two child slots whose types are compared, and what it
        // says about them. The `type of` phrasing is the one a child body draws with, so one reading covers both.
        notations.Add(TypeRuleConceptKeys.NotComparable, ed => ed.Line(
            ed.Keyword("type of"),
            ed.Space,
            ed.Property(TypeRuleLanguage.LeftSlot),
            ed.Space,
            ed.Keyword("is not comparable with"),
            ed.Space,
            ed.Keyword("type of"),
            ed.Space,
            ed.Property(TypeRuleLanguage.RightSlot)));
    }
}
