using Projectional.Language;

namespace Projectional.Samples.StateMachine;

/// <summary>
/// How a state machine looks: a braced block, and the same machine read as a table of its transitions.
/// </summary>
/// <remarks>
/// <para>
/// Two notations for one concept, chosen at projection time and never persisted
/// ([ADR 0012](../../../docs/adr/0012-concept-rendered-more-than-one-way.md)). The block form answers "what is
/// this machine made of"; the table answers "what happens when", which is the question a reader of a state
/// machine usually has - the same argument the pilot makes for a rule table.
/// </para>
/// <para>
/// <c>final</c> is a <see cref="ConditionalNotationCell"/>, so the word appears only when the state is final:
/// a state that is not final shows its name and nothing else, which is what the conditional cell is for in a
/// real language rather than in a notation test.
/// </para>
/// </remarks>
internal static class StateMachineNotations
{
    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(SmConceptKeys.Machine, ed => ed.Sequence(
            ed.Line(ed.Keyword("machine", TextEmphasis.Bold), ed.Space, ed.Property("name"), ed.Space, ed.Punct("{")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("initial:"), ed.Space, ed.Reference(StateMachineLanguage.InitialSlot)),
                ed.Line(ed.Keyword("states:")),
                ed.Indent(ed.Children(StateMachineLanguage.StatesSlot, "<none>", stacked: true)),
                ed.Line(ed.Keyword("transitions:")),
                ed.Indent(ed.Children(StateMachineLanguage.TransitionsSlot, "<none>", stacked: true)))),
            ed.Line(ed.Punct("}"))));

        // The same machine as a table over its transitions: a reader comparing events and guards sees
        // columns instead of a sequence of lines. Sources and targets are *not* columns, because a table
        // column names a property or a single child - a reference has no cell shape the grid can give it
        // (NotationRegistry.ValidateTable), which is a gap this sample records rather than works around.
        notations.Add(SmConceptKeys.Machine, StateMachineLanguage.TableVariant, ed => ed.Sequence(
            ed.Line(ed.Keyword("machine", TextEmphasis.Bold), ed.Space, ed.Property("name")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("initial:"), ed.Space, ed.Reference(StateMachineLanguage.InitialSlot)),
                ed.Table(
                    StateMachineLanguage.TransitionsSlot,
                    "+ transition",
                    ed.Column("event", ed.Keyword("event")),
                    ed.Column("guard", ed.Keyword("when")))))));

        // `final` only where it is true: a non-final state draws its name and no word at all, rather than an
        // empty cell the caret would stop in.
        notations.Add(SmConceptKeys.State, ed => ed.Line(
            ed.Keyword("state", TextEmphasis.Bold),
            ed.Space,
            ed.Property("name"),
            ed.Conditional("isFinal", "true", ed.Space, ed.Keyword("final", TextEmphasis.Italic))));

        notations.Add(SmConceptKeys.Transition, ed => ed.Line(
            ed.Keyword("on", TextEmphasis.Italic),
            ed.Space,
            ed.Property("event"),
            ed.Space,
            ed.Keyword("from"),
            ed.Space,
            ed.Reference("source"),
            ed.Space,
            ed.Keyword("to"),
            ed.Space,
            ed.Reference("target"),
            ed.Space,
            ed.Keyword("when"),
            ed.Space,
            ed.ChildInline("guard")));
    }
}
