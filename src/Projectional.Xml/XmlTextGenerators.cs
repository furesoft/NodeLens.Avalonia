using Projectional.Language;

namespace Projectional.Xml;

/// <summary>The XML document as the file it is: the model, printed with the markup it stands for.</summary>
/// <remarks>
/// <para>
/// Nothing is escaped here, and that is the point of storing the file's spelling: <c>Xml.Text.text</c> and an attribute's
/// value already hold <c>&amp;amp;</c> where the file had it (<see cref="XmlEscaping"/> applied it on the way in), so a
/// template emits a slot verbatim. A model holding the decoded value would need a part that escapes as XML, which is the
/// second answer to "how is this value written" that this design keeps out.
/// </para>
/// <para>
/// <b>An empty element is written <c>&lt;a&gt;&lt;/a&gt;</c>, not <c>&lt;a/&gt;</c>.</b> A model that holds one shape for
/// an element with no content cannot also remember which of the two spellings the file used, and a reference language
/// must not have two spellings for one tree - a document loaded from the short form comes back in the long one, which
/// every XML consumer reads as the same document.
/// </para>
/// <para>
/// The gap between two children is emitted by <see cref="ChildrenPart"/> with an empty separator, because whitespace in
/// mixed content is content: the model's own text nodes are the only thing that separates them.
/// </para>
/// </remarks>
public static class XmlTextGenerators
{
    /// <summary>The name of the generator a host picks from a menu.</summary>
    public const string XmlName = "Xml";

    /// <summary>One XML generator, which is the only direction this language generates in.</summary>
    public static TextGenerator Xml { get; } = new(XmlName, TemplateRegistry.Empty
        .Add(XmlConceptKeys.Element, TextTemplate.Of(
            new TextPart("<"),
            new PropertyPart(XmlLanguage.NameSlot),
            new WhenFilledPart(
                XmlLanguage.AttributesSlot,
                TextTemplate.Of(
                    new TextPart(" "),
                    new ChildrenPart(XmlLanguage.AttributesSlot, TextTemplate.Of(new TextPart(" "))))),
            new TextPart(">"),
            new ChildrenPart(XmlLanguage.ChildrenSlot, TextTemplate.Empty),
            new TextPart("</"),
            new PropertyPart(XmlLanguage.NameSlot),
            new TextPart(">")))

        .Add(XmlConceptKeys.Attribute, TextTemplate.Of(
            new PropertyPart(XmlLanguage.NameSlot),
            new TextPart("=\""),
            new PropertyPart(XmlLanguage.ValueSlot),
            new TextPart("\"")))

        .Add(XmlConceptKeys.Text, TextTemplate.Of(new PropertyPart(XmlLanguage.TextSlot)))

        .Add(XmlConceptKeys.Comment, TextTemplate.Of(
            new TextPart("<!--"),
            new PropertyPart(XmlLanguage.TextSlot),
            new TextPart("-->")))

        .Add(XmlConceptKeys.Raw, TextTemplate.Of(new PropertyPart(XmlLanguage.TextSlot))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Xml];
}
