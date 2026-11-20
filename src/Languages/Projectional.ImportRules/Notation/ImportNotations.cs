using Projectional.Language;

namespace Projectional.ImportRules;

/// <summary>How a declared importer is drawn: one sentence per rule and per token class.</summary>
/// <remarks>
/// A language project's <c>import</c> folder holds models, so without a notation an author could load one and not read
/// it - the point of a declared aspect is that the document is the declaration and the document is drawn. The drawings
/// keep the vocabulary's tone: a literal is quoted text, a capture names the property it fills and the class it reads,
/// and the parts of a rule are one line each so a rule reads as the sequence it is.
/// </remarks>
public static class ImportNotations
{
    private const string NoToken = "<< no token class >>";
    private const string NoRule = "<< no rule >>";
    private const string NoPart = "<< no part >>";
    private const string NoProperty = "<< which property >>";
    private const string NoClass = "<< which class >>";
    private const string NoText = "<< text >>";
    private const string NoMark = "<< mark >>";
    private const string NoName = "<< which name >>";

    /// <summary>Registers the notation of every concept in the import language.</summary>
    /// <param name="notations">The registry to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(ImportConceptKeys.Importer, ed => ed.Sequence(
            ed.Line(ed.Keyword("importer"), ed.Space, ed.Property(ImportLanguage.NameSlot, emptyText: NoName)),
            ed.Indent(ed.Children(ImportLanguage.TokensSlot, NoToken, stacked: true)),
            ed.Indent(ed.Children(ImportLanguage.RulesSlot, NoRule, stacked: true))));

        notations.Add(ImportConceptKeys.Rule, ed => ed.Sequence(
            ed.Line(ed.Keyword("rule"), ed.Space, ed.Property(ImportLanguage.ConceptKeySlot, emptyText: NoName)),
            ed.Indent(ed.Children(ImportLanguage.PartsSlot, NoPart, stacked: true))));

        notations.Add(ImportConceptKeys.Literal, ed => ed.Line(
            ed.Punct("\""), ed.Property(ImportLanguage.TextSlot, emptyText: NoText), ed.Punct("\"")));

        notations.Add(ImportConceptKeys.Capture, ed => ed.Line(
            ed.Keyword("capture"), ed.Space, ed.Property(ImportLanguage.SlotSlot, emptyText: NoProperty),
            ed.Space, ed.Keyword("from"), ed.Space, ed.Property(ImportLanguage.TokenSlot, emptyText: NoClass)));

        notations.Add(ImportConceptKeys.Nest, ed => ed.Line(
            ed.Keyword("nest"), ed.Space, ed.Property(ImportLanguage.SlotSlot, emptyText: NoProperty)));

        notations.Add(ImportConceptKeys.Repeat, ed => ed.Line(
            ed.Keyword("repeat"), ed.Space, ed.Property(ImportLanguage.SlotSlot, emptyText: NoProperty),
            ed.Space, ed.Keyword("separated by"), ed.Space, ed.Punct("\""),
            ed.Property(ImportLanguage.SeparatorSlot, emptyText: NoText), ed.Punct("\""),
            ed.Space, ed.Keyword("at least"), ed.Space, ed.Property(ImportLanguage.MinimumSlot)));

        notations.Add(ImportConceptKeys.Raw, ed => ed.Line(
            ed.Keyword("raw"), ed.Space, ed.Property(ImportLanguage.SlotSlot, emptyText: NoProperty),
            ed.Space, ed.Keyword("until"), ed.Space, ed.Punct("\""),
            ed.Property(ImportLanguage.UntilSlot, emptyText: NoText), ed.Punct("\"")));

        notations.Add(ImportConceptKeys.Bind, ed => ed.Line(
            ed.Keyword("bind"), ed.Space, ed.Property(ImportLanguage.AsSlot, emptyText: NoName),
            ed.Space, ed.Keyword("from"), ed.Space, ed.Property(ImportLanguage.TokenSlot, emptyText: NoClass)));

        notations.Add(ImportConceptKeys.Matches, ed => ed.Line(
            ed.Keyword("must be"), ed.Space, ed.Property(ImportLanguage.AsSlot, emptyText: NoName),
            ed.Space, ed.Keyword("again")));

        notations.Add(ImportConceptKeys.Fail, ed => ed.Line(
            ed.Keyword("fail"), ed.Space, ed.Property(ImportLanguage.CodeSlot, emptyText: NoName),
            ed.Space, ed.Punct("\""), ed.Property(ImportLanguage.MessageSlot, emptyText: NoText), ed.Punct("\"")));

        notations.Add(ImportConceptKeys.Word, ed => ed.Line(
            ed.Keyword("token"), ed.Space, ed.Property(ImportLanguage.TokenNameSlot, emptyText: NoName),
            ed.Space, ed.Keyword("is a word, and also accepts"), ed.Space,
            ed.Property(ImportLanguage.ExtraSlot, emptyText: NoText)));

        notations.Add(ImportConceptKeys.Number, ed => ed.Line(
            ed.Keyword("token"), ed.Space, ed.Property(ImportLanguage.TokenNameSlot, emptyText: NoName),
            ed.Space, ed.Keyword("is a number")));

        notations.Add(ImportConceptKeys.Quoted, ed => ed.Line(
            ed.Keyword("token"), ed.Space, ed.Property(ImportLanguage.TokenNameSlot, emptyText: NoName),
            ed.Space, ed.Keyword("stands between"), ed.Space,
            ed.Property(ImportLanguage.QuoteSlot, emptyText: NoMark), ed.Space,
            ed.Keyword("and resolves"), ed.Space,
            ed.Property(ImportLanguage.EscapeSlot, emptyText: NoMark)));

        notations.Add(ImportConceptKeys.Fixed, ed => ed.Line(
            ed.Keyword("token"), ed.Space, ed.Property(ImportLanguage.TokenNameSlot, emptyText: NoName),
            ed.Space, ed.Keyword("stands between"), ed.Space,
            ed.Property(ImportLanguage.OpenSlot, emptyText: NoMark), ed.Space,
            ed.Keyword("and"), ed.Space,
            ed.Property(ImportLanguage.CloseSlot, emptyText: NoMark), ed.Space,
            ed.Keyword("resolving nothing")));

        notations.Add(ImportConceptKeys.Skipped, ed => ed.Line(
            ed.Keyword("token"), ed.Space, ed.Property(ImportLanguage.TokenNameSlot, emptyText: NoName),
            ed.Space, ed.Keyword("is skipped, starting at"), ed.Space, ed.Punct("\""),
            ed.Property(ImportLanguage.PrefixSlot, emptyText: NoText), ed.Punct("\""),
            ed.Space, ed.Keyword("until"), ed.Space, ed.Punct("\""),
            ed.Property(ImportLanguage.UntilSlot, emptyText: NoText), ed.Punct("\"")));
    }
}
