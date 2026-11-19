namespace Projectional.Xml;

/// <summary>The keys of the XML language: an element, three things it can hold, and one escape hatch.</summary>
/// <remarks>
/// Six concepts and no document node, for the reason the JSON language has none: a document <i>is</i> its top-level
/// nodes. XML allows more than one of those - a comment before the root element and another after it are ordinary - so
/// the importer answers several roots rather than inventing a container to hold them.
/// </remarks>
public static class XmlConceptKeys
{
    /// <summary>Anything an element can hold.</summary>
    public const string Node = "Xml.Node";

    /// <summary>An element: a name, its attributes and its content.</summary>
    public const string Element = "Xml.Element";

    /// <summary>One attribute: a name and the value it was written with.</summary>
    public const string Attribute = "Xml.Attribute";

    /// <summary>Character data, whitespace-only runs included.</summary>
    public const string Text = "Xml.Text";

    /// <summary>A comment.</summary>
    public const string Comment = "Xml.Comment";

    /// <summary>The construct this language does not model, kept as the text it was written as.</summary>
    public const string Raw = "Xml.Raw";
}
