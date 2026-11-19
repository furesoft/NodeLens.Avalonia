using Projectional.Language;

namespace Projectional.Xml;

/// <summary>How XML is drawn: the markup the file has, with the parts of it that are data in cells.</summary>
/// <remarks>
/// <para>
/// Every concept is one <see cref="LineNotationCell"/>, so a document draws on one line, as the JSON language does and
/// for the same reason: the layout engine does not wrap, and a projection that re-broke the lines would show a shape
/// the file does not have. Mixed content makes that more than a convenience - the gap between two children here is
/// <em>content</em>, a text node the file has - so the children collection is drawn with no spacing of its own and the
/// whitespace the model holds is what separates them.
/// </para>
/// <para>
/// An element shows its name twice, once in each tag, because that is what the file says; both cells address the same
/// slot, so editing either renames the element and the rewrite shows it in the other. A closing tag whose name depended
/// on something else could disagree with the opening one.
/// </para>
/// <para>
/// <b>Both collections are hidden while they hold nothing</b>, which is the one place this notation differs from the
/// JSON one. In XML an empty slot cannot be drawn as a blank: whitespace inside an element <em>is</em> content, so an
/// empty element that showed a placeholder would be indistinguishable from one holding a space, and telling those two
/// apart is the whole job of a language that mirrors a file. The price is that the first attribute and the first child
/// have no caret stop of their own - the two intentions that would create them are not in this slice, and documents are
/// edited in what they already draw until they arrive.
/// </para>
/// </remarks>
internal static class XmlNotations
{
    /// <summary>An empty collection draws one cell of no width of its own, so the slot stays a caret stop.</summary>
    private const string Empty = "";

    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(XmlConceptKeys.Element, ed => ed.Line(
            ed.Punct("<"),
            ed.Property(XmlLanguage.NameSlot, emptyText: "element"),
            ed.WhenFilled(
                XmlLanguage.AttributesSlot,
                ed.Space,
                ed.InlineCollection(XmlLanguage.AttributesSlot, ed.Space, Empty, spacing: 0)),
            ed.Punct(">"),
            ed.WhenFilled(
                XmlLanguage.ChildrenSlot,
                ed.InlineCollection(XmlLanguage.ChildrenSlot, emptyText: Empty, spacing: 0)),
            ed.Punct("</"),
            ed.Property(XmlLanguage.NameSlot),
            ed.Punct(">")));

        notations.Add(XmlConceptKeys.Attribute, ed => ed.Line(
            ed.Property(XmlLanguage.NameSlot, emptyText: "name"),
            ed.Punct("=\""),
            ed.Property(XmlLanguage.ValueSlot),
            ed.Punct("\"")));

        notations.Add(XmlConceptKeys.Text, ed => ed.Line(ed.Property(XmlLanguage.TextSlot)));

        notations.Add(XmlConceptKeys.Comment, ed => ed.Line(
            ed.Punct("<!--"),
            ed.Property(XmlLanguage.TextSlot, emptyText: "comment"),
            ed.Punct("-->")));

        notations.Add(XmlConceptKeys.Raw, ed => ed.Line(ed.Property(XmlLanguage.TextSlot)));
    }
}
