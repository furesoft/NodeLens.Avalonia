using Projectional.Language;

namespace Projectional.Yaml;

/// <summary>The YAML document as the file it is: the model, printed with the layout the model chose.</summary>
/// <remarks>
/// <para>
/// A scalar is emitted verbatim, because its slot holds the whole spelling - the quotes, the escapes, an anchor, a block
/// scalar's indicator and body. That is what makes the round trip exact without an escaping part, and it is why editing a
/// scalar means typing the spelling the file would have.
/// </para>
/// <para>
/// A pair's value is emitted on the key's line or on the next one, decided by
/// <see cref="YamlLanguage.LayoutSlot"/>. A block collection needs the next line, and no part can ask what kind of node a
/// child is, so the model carries that fact - the same shape TOML uses for "a table remembers whether it is a header or
/// inline".
/// </para>
/// <para>
/// A sequence item is <c>-</c> and the value one level deeper, so a block mapping inside an item keeps its own lines
/// aligned under the dash rather than under the sequence.
/// </para>
/// </remarks>
public static class YamlTextGenerators
{
    /// <summary>The name of the generator a host picks from a menu.</summary>
    public const string YamlName = "YAML";

    /// <summary>One YAML generator, which is the only direction this language generates in.</summary>
    public static TextGenerator Yaml { get; } = new(YamlName, TemplateRegistry.Empty
        .Add(YamlConceptKeys.Document, TextTemplate.Of(
            new ChildrenPart(YamlLanguage.EntriesSlot, TextTemplate.Of(LineBreakPart.Instance))))

        .Add(YamlConceptKeys.Mapping, TextTemplate.Of(
            new ChildrenPart(YamlLanguage.MembersSlot, TextTemplate.Of(LineBreakPart.Instance))))

        .Add(YamlConceptKeys.Sequence, TextTemplate.Of(
            new ChildrenPart(YamlLanguage.ItemsSlot, TextTemplate.Of(LineBreakPart.Instance))))

        .Add(YamlConceptKeys.SequenceItem, TextTemplate.Of(
            new TextPart("-"),
            new IndentPart(TextTemplate.Of(new TextPart(" "), new ChildPart(YamlLanguage.ValueSlot)))))

        .Add(YamlConceptKeys.Pair, TextTemplate.Of(
            new ChildPart(YamlLanguage.KeySlot),
            new TextPart(":"),
            new WhenFilledPart(
                YamlLanguage.ValueSlot,
                TextTemplate.Of(
                    new ConditionalPart(
                        YamlLanguage.LayoutSlot,
                        YamlLanguage.Nested,
                        TextTemplate.Of(
                            new IndentPart(TextTemplate.Of(
                                LineBreakPart.Instance,
                                new ChildPart(YamlLanguage.ValueSlot)))),
                        TextTemplate.Of(
                            new TextPart(" "),
                            new ChildPart(YamlLanguage.ValueSlot)))))))

        .Add(YamlConceptKeys.FlowMapping, TextTemplate.Of(
            new TextPart("{"),
            new WhenFilledPart(
                YamlLanguage.MembersSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(YamlLanguage.MembersSlot, TextTemplate.Of(new TextPart(","), new TextPart(" "))),
                    new TextPart(" "))),
            new TextPart("}")))

        .Add(YamlConceptKeys.FlowPair, TextTemplate.Of(
            new ChildPart(YamlLanguage.KeySlot),
            new TextPart(": "),
            new ChildPart(YamlLanguage.ValueSlot)))

        .Add(YamlConceptKeys.FlowSequence, TextTemplate.Of(
            new TextPart("["),
            new WhenFilledPart(
                YamlLanguage.ItemsSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(YamlLanguage.ItemsSlot, TextTemplate.Of(new TextPart(","), new TextPart(" "))),
                    new TextPart(" "))),
            new TextPart("]")))

        .Add(YamlConceptKeys.Scalar, TextTemplate.Of(new PropertyPart(YamlLanguage.TextSlot)))

        .Add(YamlConceptKeys.Alias, TextTemplate.Of(
            new TextPart("*"),
            new PropertyPart(YamlLanguage.NameSlot)))

        .Add(YamlConceptKeys.Comment, TextTemplate.Of(
            new TextPart("#"),
            new PropertyPart(YamlLanguage.TextSlot)))

        .Add(YamlConceptKeys.Raw, TextTemplate.Of(new PropertyPart(YamlLanguage.TextSlot))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Yaml];
}
