using Projectional.Language;

namespace Projectional.IntentionRules.Notation;

/// <summary>How an intention is drawn: one sentence per rule, and one per body.</summary>
/// <remarks>
/// Both bodies are single lines, because <c>ChildInline</c> refuses a block: the rule's line names the entry and then
/// shows what it does, which is how a reader checks a definition without opening the language it belongs to. The
/// presets are an inline list for the same reason - a wrap is read as one sentence.
/// </remarks>
public static class IntentionNotations
{
    /// <summary>Registers the intention language's own notation.</summary>
    /// <param name="notations">The registry to add to, which must know the intention language's concepts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(IntentionConceptKeys.Rule, ed => ed.Line(
            ed.Keyword("intention"),
            ed.Space,
            ed.Property(IntentionLanguage.LabelSlot, emptyText: "<label>"),
            ed.Space,
            ed.Keyword("on"),
            ed.Space,
            ed.Property(IntentionLanguage.ConceptKeySlot, emptyText: "*"),
            ed.Space,
            ed.Keyword("does"),
            ed.Space,
            ed.ChildInline(IntentionLanguage.ActionSlot)));

        notations.Add(IntentionConceptKeys.Wrap, ed => ed.Line(
            ed.Keyword("wrap"),
            ed.Space,
            ed.Keyword("in"),
            ed.Space,
            ed.Property(IntentionLanguage.WrapperSlot, emptyText: "<wrapper>"),
            ed.Punct("."),
            ed.Property(IntentionLanguage.WrapperChildSlot, emptyText: "<slot>"),
            ed.Space,
            ed.Keyword("with"),
            ed.Space,
            ed.InlineCollection(IntentionLanguage.PresetSlot, ed.Punct(","), emptyText: "<none>")));

        notations.Add(IntentionConceptKeys.Preset, ed => ed.Line(
            ed.Property(IntentionLanguage.PresetPropertySlot, emptyText: "<slot>"),
            ed.Space,
            ed.Punct("="),
            ed.Space,
            ed.Property(IntentionLanguage.PresetValueSlot, emptyText: "<value>")));

        notations.Add(IntentionConceptKeys.InsertSibling, ed => ed.Line(
            ed.Keyword("insert"),
            ed.Space,
            ed.Property(IntentionLanguage.SideSlot)));

        notations.Add(
            IntentionConceptKeys.Insert,
            ed => ed.Line(
                ed.Keyword("insert"),
                ed.Space,
                ed.Property(IntentionLanguage.ConceptSlot, emptyText: "<concept>")));

        notations.Add(
            IntentionConceptKeys.Replace,
            ed => ed.Line(
                ed.Keyword("replace with"),
                ed.Space,
                ed.Property(IntentionLanguage.WithSlot, emptyText: "<concept>")));

        notations.Add(IntentionConceptKeys.Delete, ed => ed.Line(ed.Keyword("delete")));

        notations.Add(IntentionConceptKeys.Move, ed => ed.Line(
            ed.Keyword("move"),
            ed.Space,
            ed.Property(IntentionLanguage.MoveSlot)));

        notations.Add(IntentionConceptKeys.Set, ed => ed.Line(
            ed.Keyword("set"),
            ed.Space,
            ed.Property(IntentionLanguage.SetSlot, emptyText: "<slot>"),
            ed.Space,
            ed.Punct("="),
            ed.Space,
            ed.Property(IntentionLanguage.SetValueSlot, emptyText: "<value>")));
    }
}
