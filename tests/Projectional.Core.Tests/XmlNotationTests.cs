using Projectional.Core;
using Projectional.Projection;
using Projectional.Xml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>How an XML document is drawn: the markup of the file, with its data in cells.</summary>
public sealed class XmlNotationTests
{
    [Fact]
    public void AnElementIsDrawnAsTheFileReads()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");

        var attribute = model.NewChild(element, XmlLanguage.AttributesSlot, XmlConceptKeys.Attribute);
        attribute.Set(XmlLanguage.NameSlot, "x");
        attribute.Set(XmlLanguage.ValueSlot, "1");

        model.NewChild(element, XmlLanguage.ChildrenSlot, XmlConceptKeys.Text)
            .Set(XmlLanguage.TextSlot, "t");

        Assert.Equal("<a x=\"1\">t</a>", Render(element));
    }

    [Fact]
    public void AnElementWithNoContentShowsItsTwoTags()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");

        Assert.Equal("<a></a>", Render(element));
    }

    [Fact]
    public void WhitespaceBetweenTwoChildrenIsDrawnAsContent()
    {
        // The gap is a text node, not layout: the notation adds no spacing of its own, so what the reader sees is what
        // the file holds.
        var model = XmlLanguage.NewProject().NewModel("xml");
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");
        model.NewChild(element, XmlLanguage.ChildrenSlot, XmlConceptKeys.Text).Set(XmlLanguage.TextSlot, " ");
        model.NewChild(element, XmlLanguage.ChildrenSlot, XmlConceptKeys.Element).Set(XmlLanguage.NameSlot, "b");

        Assert.Equal("<a> <b></b></a>", Render(element));
    }

    [Fact]
    public void ANamespaceDeclarationIsDrawnLikeAnyOtherAttribute()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "p:a");

        var declaration = model.NewChild(element, XmlLanguage.AttributesSlot, XmlConceptKeys.Attribute);
        declaration.Set(XmlLanguage.NameSlot, "xmlns:p");
        declaration.Set(XmlLanguage.ValueSlot, "urn:x");

        Assert.Equal("<p:a xmlns:p=\"urn:x\"></p:a>", Render(element));
    }

    private static string Render(Node node) =>
        LayoutSnapshot.Render(node, XmlLanguage.Notations, width: 120).TrimEnd();
}
