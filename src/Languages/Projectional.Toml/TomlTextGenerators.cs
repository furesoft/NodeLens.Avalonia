using Projectional.Language;

namespace Projectional.Toml;

/// <summary>The TOML document as the file it is: the model, printed with the format's own lines.</summary>
/// <remarks>
/// <para>
/// Nothing is escaped here, and that is the point of storing the file's spelling: a string's text is what sat between
/// its delimiters, an integer is its literal, a date is its text. A language holding decoded values would need a part
/// that escapes as TOML, which is the second answer to "how is this value written" the design keeps out of the template
/// vocabulary.
/// </para>
/// <para>
/// <b>The document's order is emitted as it stands.</b> Entries are joined by a line break, so <c>[a]</c>, then its
/// pairs, then <c>[[b]]</c> come out in that order - a template that sorted or grouped would rewrite the file.
/// </para>
/// <para>
/// <b>A string's delimiters come from its kind</b>, through <see cref="ConditionalPart"/>: three quotes for a
/// multi-line string, one quote for a literal one, and nothing at all where the condition does not hold - which is why
/// one concept spells four syntaxes without a second concept or a delegate.
/// </para>
/// </remarks>
public static class TomlTextGenerators
{
    /// <summary>The name of the generator a host picks from a menu.</summary>
    public const string TomlName = "TOML";

    /// <summary>One TOML generator, which is the only direction this language generates in.</summary>
    public static TextGenerator Toml { get; } = new(TomlName, TemplateRegistry.Empty
        .Add(TomlConceptKeys.Document, TextTemplate.Of(
            new ChildrenPart(TomlLanguage.EntriesSlot, TextTemplate.Of(LineBreakPart.Instance)),

            // A file ends with a line break, but an empty document is an empty file: the newline is conditional on
            // there being an entry, which is the same question its separator asks.
            new WhenFilledPart(TomlLanguage.EntriesSlot, TextTemplate.Of(LineBreakPart.Instance))))

        .Add(TomlConceptKeys.KeyValue, TextTemplate.Of(
            new PropertyPart(TomlLanguage.KeySlot),
            new TextPart(" = "),
            new ChildPart(TomlLanguage.ValueSlot)))

        .Add(TomlConceptKeys.Table, TextTemplate.Of(
            new TextPart("["),
            new PropertyPart(TomlLanguage.KeySlot),
            new TextPart("]"),
            new IndentPart(TextTemplate.Of(new WhenFilledPart(
                TomlLanguage.EntriesSlot,
                TextTemplate.Of(
                    LineBreakPart.Instance,
                    new ChildrenPart(TomlLanguage.EntriesSlot, TextTemplate.Of(LineBreakPart.Instance))))))))

        .Add(TomlConceptKeys.ArrayOfTables, TextTemplate.Of(
            new TextPart("[["),
            new PropertyPart(TomlLanguage.KeySlot),
            new TextPart("]]"),
            new IndentPart(TextTemplate.Of(new WhenFilledPart(
                TomlLanguage.EntriesSlot,
                TextTemplate.Of(
                    LineBreakPart.Instance,
                    new ChildrenPart(TomlLanguage.EntriesSlot, TextTemplate.Of(LineBreakPart.Instance))))))))

        .Add(TomlConceptKeys.Comment, TextTemplate.Of(
            new TextPart("#"),
            new PropertyPart(TomlLanguage.TextSlot)))

        .Add(TomlConceptKeys.String, TextTemplate.Of(
            Delimiter(TomlLanguage.BasicString, "\""),
            Delimiter(TomlLanguage.MultilineBasicString, "\"\"\""),
            Delimiter(TomlLanguage.LiteralString, "'"),
            Delimiter(TomlLanguage.MultilineLiteralString, "'''"),
            new PropertyPart(TomlLanguage.TextSlot),
            Delimiter(TomlLanguage.BasicString, "\""),
            Delimiter(TomlLanguage.MultilineBasicString, "\"\"\""),
            Delimiter(TomlLanguage.LiteralString, "'"),
            Delimiter(TomlLanguage.MultilineLiteralString, "'''")))

        .Add(TomlConceptKeys.Integer, TextTemplate.Of(new PropertyPart(TomlLanguage.TextSlot)))

        .Add(TomlConceptKeys.Float, TextTemplate.Of(new PropertyPart(TomlLanguage.TextSlot)))

        .Add(TomlConceptKeys.DateTime, TextTemplate.Of(new PropertyPart(TomlLanguage.TextSlot)))

        .Add(TomlConceptKeys.Bool, TextTemplate.Of(new PropertyPart(TomlLanguage.ValueSlot)))

        .Add(TomlConceptKeys.Array, TextTemplate.Of(
            new TextPart("["),
            new WhenFilledPart(
                TomlLanguage.ItemsSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(TomlLanguage.ItemsSlot, TextTemplate.Of(new TextPart(","), new TextPart(" "))),
                    new TextPart(" "))),
            new TextPart("]")))

        .Add(TomlConceptKeys.InlineTable, TextTemplate.Of(
            new TextPart("{"),
            new WhenFilledPart(
                TomlLanguage.MembersSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(TomlLanguage.MembersSlot, TextTemplate.Of(new TextPart(","), new TextPart(" "))),
                    new TextPart(" "))),
            new TextPart("}"))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Toml];

    /// <summary>The delimiter a string of <paramref name="kind"/> is written in, or nothing for another kind.</summary>
    private static ConditionalPart Delimiter(string kind, string delimiter) =>
        new(TomlLanguage.KindSlot, kind, TextTemplate.Of(new TextPart(delimiter)));
}
