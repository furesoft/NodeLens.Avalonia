using Projectional.Language;

namespace Projectional.Json;

/// <summary>The JSON document as the file it is: the model, printed with the format's own punctuation.</summary>
/// <remarks>
/// <para>
/// The escaping question does not arise here, and that is the point of holding a string as the file spells it: the value
/// is already legal JSON text, so the template emits it verbatim. A language that held the unescaped value would need a
/// part that escapes as JSON, and a format's spelling inside the framework's template vocabulary is exactly what this
/// design keeps out (docs/architecture/import.md).
/// </para>
/// <para>
/// The brackets are emitted always and the space inside them is the <see cref="WhenFilledPart"/>: an object with no
/// member must print <c>{}</c>, which would not be true of a template that put the whole bracket pair inside the
/// conditional. An array of one item prints <c>[ 1 ]</c>, so the drawing and the generated text differ only where a
/// slot must stay visible to the caret.
/// </para>
/// </remarks>
public static class JsonTextGenerators
{
    /// <summary>The name of the generator a host picks from a menu.</summary>
    public const string JsonName = "Json";

    /// <summary>One JSON generator, which is the only direction this language generates in.</summary>
    public static TextGenerator Json { get; } = new(JsonName, TemplateRegistry.Empty
        .Add(JsonConceptKeys.Object, TextTemplate.Of(
            new TextPart("{"),
            new WhenFilledPart(
                JsonLanguage.MembersSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(JsonLanguage.MembersSlot, TextTemplate.Of(new TextPart(","), new TextPart(" "))),
                    new TextPart(" "))),
            new TextPart("}")))

        .Add(JsonConceptKeys.Property, TextTemplate.Of(
            new TextPart("\""),
            new PropertyPart(JsonLanguage.NameSlot),
            new TextPart("\": "),
            new ChildPart(JsonLanguage.ValueSlot)))

        .Add(JsonConceptKeys.Array, TextTemplate.Of(
            new TextPart("["),
            new WhenFilledPart(
                JsonLanguage.ItemsSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(JsonLanguage.ItemsSlot, TextTemplate.Of(new TextPart(","), new TextPart(" "))),
                    new TextPart(" "))),
            new TextPart("]")))

        .Add(JsonConceptKeys.Text, TextTemplate.Of(
            new TextPart("\""),
            new PropertyPart(JsonLanguage.TextSlot),
            new TextPart("\"")))

        .Add(JsonConceptKeys.Number, TextTemplate.Of(new PropertyPart(JsonLanguage.TextSlot)))

        .Add(JsonConceptKeys.Bool, TextTemplate.Of(new PropertyPart(JsonLanguage.ValueSlot)))

        .Add(JsonConceptKeys.Null, TextTemplate.Of(new TextPart("null"))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Json];
}
