using Projectional.Core;
using Projectional.Language;
using Projectional.Xml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>XML out: the document as the file spells it.</summary>
public sealed class XmlTextGeneratorsTests
{
    [Fact]
    public void AnElementIsItsNameItsAttributesAndItsContent()
    {
        var model = NewModel();
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");

        var attribute = model.NewChild(element, XmlLanguage.AttributesSlot, XmlConceptKeys.Attribute);
        attribute.Set(XmlLanguage.NameSlot, "x");
        attribute.Set(XmlLanguage.ValueSlot, "1");

        model.NewChild(element, XmlLanguage.ChildrenSlot, XmlConceptKeys.Text)
            .Set(XmlLanguage.TextSlot, "t");

        Assert.Equal("<a x=\"1\">t</a>", Generate(element));
    }

    [Fact]
    public void AnEmptyElementIsWrittenInTheLongForm()
    {
        // One shape for one tree: the model cannot remember whether the file used "<a/>", so the language picks one
        // spelling and a document loaded from the short form comes back in the long one.
        var model = NewModel();
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");

        Assert.Equal("<a></a>", Generate(element));
    }

    [Fact]
    public void ATextSlotIsEmittedVerbatim()
    {
        // The escaping happened when the value entered the model, so generation must not do it a second time.
        var model = NewModel();
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");
        model.NewChild(element, XmlLanguage.ChildrenSlot, XmlConceptKeys.Text)
            .Set(XmlLanguage.TextSlot, "a &amp; b");

        Assert.Equal("<a>a &amp; b</a>", Generate(element));
    }

    [Fact]
    public void ChildrenAreSeparatedByNothingTheGeneratorAdds()
    {
        // Whitespace in mixed content is content, so the only gap between two children is a text node of the model.
        var model = NewModel();
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");
        model.NewChild(element, XmlLanguage.ChildrenSlot, XmlConceptKeys.Text).Set(XmlLanguage.TextSlot, " ");
        model.NewChild(element, XmlLanguage.ChildrenSlot, XmlConceptKeys.Element).Set(XmlLanguage.NameSlot, "b");

        Assert.Equal("<a> <b></b></a>", Generate(element));
    }

    [Fact]
    public void ACommentIsItsMarkersAndItsText()
    {
        var model = NewModel();
        var comment = model.NewNode(XmlConceptKeys.Comment);
        comment.Set(XmlLanguage.TextSlot, " note ");

        Assert.Equal("<!-- note -->", Generate(comment));
    }

    [Fact]
    public void ARawNodeIsItsText()
    {
        var model = NewModel();
        var raw = model.NewNode(XmlConceptKeys.Raw);
        raw.Set(XmlLanguage.TextSlot, "<![CDATA[x]]>");

        Assert.Equal("<![CDATA[x]]>", Generate(raw));
    }

    [Fact]
    public void TheGeneratorIsNamedSoAMenuCanListIt()
    {
        Assert.Equal("Xml", XmlTextGenerators.Xml.Name);
        Assert.Same(XmlTextGenerators.Xml, Assert.Single(XmlTextGenerators.All));
    }

    private static string Generate(Node node)
    {
        var generated = new Projectional.Semantics.Generator().Generate(node, XmlTextGenerators.Xml);

        Assert.True(generated.Succeeded, generated.ToString());
        return generated.Text;
    }

    private static Model NewModel() => XmlLanguage.NewProject().NewModel("xml");
}
