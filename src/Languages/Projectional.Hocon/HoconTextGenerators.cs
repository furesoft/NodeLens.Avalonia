using Projectional.Language;

namespace Projectional.Hocon;

/// <summary>The HOCON document as the file it is: the model, printed with the format's own separators.</summary>
/// <remarks>
/// <para>
/// Nothing is escaped here, and that is the point of storing each spelling: a quoted string's text is what sat between
/// its quotes, an unquoted one is the run itself, and a note keeps its marker. A language holding decoded values would
/// need a part that escapes as HOCON, which is the second answer to "how is this value written" the design keeps out.
/// </para>
/// <para>
/// The four separators are four <see cref="ConditionalPart"/>s on one property, so a property prints exactly the
/// separator the model says it has - including nothing but a space, which is how <c>key value</c> and
/// <c>key { ... }</c> are written.
/// </para>
/// </remarks>
public static class HoconTextGenerators
{
    /// <summary>The name of the generator a host picks from a menu.</summary>
    public const string HoconName = "HOCON";

    /// <summary>One HOCON generator, which is the only direction this language generates in.</summary>
    public static TextGenerator Hocon { get; } = new(HoconName, TemplateRegistry.Empty
        .Add(HoconConceptKeys.Document, TextTemplate.Of(
            new ChildrenPart(HoconLanguage.MembersSlot, TextTemplate.Of(LineBreakPart.Instance)),
            new WhenFilledPart(HoconLanguage.MembersSlot, TextTemplate.Of(LineBreakPart.Instance))))

        .Add(HoconConceptKeys.Object, TextTemplate.Of(
            new TextPart("{"),
            new WhenFilledPart(
                HoconLanguage.MembersSlot,
                TextTemplate.Of(
                    new IndentPart(TextTemplate.Of(
                        LineBreakPart.Instance,
                        new ChildrenPart(HoconLanguage.MembersSlot, TextTemplate.Of(LineBreakPart.Instance)))),
                    LineBreakPart.Instance)),
            new TextPart("}")))

        .Add(HoconConceptKeys.Property, TextTemplate.Of(
            new PropertyPart(HoconLanguage.KeySlot),
            Separator(HoconLanguage.EqualsOperator, " = "),
            Separator(HoconLanguage.ColonOperator, ": "),
            Separator(HoconLanguage.AppendOperator, " += "),
            Separator(HoconLanguage.SpaceOperator, " "),
            new ChildPart(HoconLanguage.ValueSlot)))

        .Add(HoconConceptKeys.Array, TextTemplate.Of(
            new TextPart("["),
            new WhenFilledPart(
                HoconLanguage.ItemsSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(HoconLanguage.ItemsSlot, TextTemplate.Of(new TextPart(","), new TextPart(" "))),
                    new TextPart(" "))),
            new TextPart("]")))

        .Add(HoconConceptKeys.String, TextTemplate.Of(
            Delimiter(HoconLanguage.Quoted, "\""),
            Delimiter(HoconLanguage.TripleQuoted, "\"\"\""),
            new PropertyPart(HoconLanguage.TextSlot),
            Delimiter(HoconLanguage.Quoted, "\""),
            Delimiter(HoconLanguage.TripleQuoted, "\"\"\"")))

        .Add(HoconConceptKeys.Number, TextTemplate.Of(new PropertyPart(HoconLanguage.LiteralSlot)))

        .Add(HoconConceptKeys.Bool, TextTemplate.Of(new PropertyPart(HoconLanguage.ValueSlot)))

        .Add(HoconConceptKeys.Null, TextTemplate.Of(new TextPart("null")))

        .Add(HoconConceptKeys.Concat, TextTemplate.Of(
            new ChildrenPart(HoconLanguage.PartsSlot, TextTemplate.Of(new TextPart(" ")))))

        .Add(HoconConceptKeys.Substitution, TextTemplate.Of(
            new TextPart("${"),
            OptionalMark(),
            new PropertyPart(HoconLanguage.PathSlot),
            new TextPart("}")))

        .Add(HoconConceptKeys.Include, TextTemplate.Of(
            new TextPart("include "),
            new PropertyPart(HoconLanguage.TargetSlot)))

        .Add(HoconConceptKeys.Comment, TextTemplate.Of(
            new ConditionalPart(HoconLanguage.MarkerSlot, "#", TextTemplate.Of(new TextPart("#")), TextTemplate.Of(new TextPart("//"))),
            new PropertyPart(HoconLanguage.TextSlot))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Hocon];

    /// <summary>The text a property prints when its separator is <paramref name="operator"/>.</summary>
    private static ConditionalPart Separator(string @operator, string spelled) =>
        new(HoconLanguage.OperatorSlot, @operator, TextTemplate.Of(new TextPart(spelled)));

    /// <summary>The delimiter of a string whose <c>quoted</c> slot holds <paramref name="quotedness"/>.</summary>
    private static ConditionalPart Delimiter(string quotedness, string delimiter) =>
        new(HoconLanguage.QuotedSlot, quotedness, TextTemplate.Of(new TextPart(delimiter)));

    /// <summary>The question mark a substitution that may resolve to nothing carries.</summary>
    private static ConditionalPart OptionalMark() =>
        new(HoconLanguage.OptionalSlot, HoconLanguage.Optional, TextTemplate.Of(new TextPart("?")));
}
