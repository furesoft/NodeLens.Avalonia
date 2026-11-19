using Projectional.Core;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Xml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>XML in: the tree the document describes, with every character of its content.</summary>
public sealed class XmlImporterTests
{
    [Fact]
    public void AnElementBecomesItsNameAttributesAndContent()
    {
        var (_, result) = Import("<a x=\"1\">t</a>");

        var root = Assert.Single(result.Roots);

        Assert.Equal(XmlConceptKeys.Element, root.Concept.Key);
        Assert.Equal("a", root.GetOrNull(XmlLanguage.NameSlot));

        var attribute = Assert.Single(root.GetChildren(XmlLanguage.AttributesSlot));
        Assert.Equal("x", attribute.GetOrNull(XmlLanguage.NameSlot));
        Assert.Equal("1", attribute.GetOrNull(XmlLanguage.ValueSlot));

        var text = Assert.Single(root.GetChildren(XmlLanguage.ChildrenSlot));
        Assert.Equal(XmlConceptKeys.Text, text.Concept.Key);
        Assert.Equal("t", text.GetOrNull(XmlLanguage.TextSlot));
    }

    [Fact]
    public void AttributesKeepTheOrderTheFileHas()
    {
        var (_, result) = Import("<a b=\"1\" a=\"2\"/>");

        var names = Assert.Single(result.Roots)
            .GetChildren(XmlLanguage.AttributesSlot)
            .Select(attribute => attribute.GetOrNull(XmlLanguage.NameSlot));

        Assert.Equal(["b", "a"], names);
    }

    [Fact]
    public void WhitespaceOnlyTextIsANode()
    {
        // In XML, whitespace between markup is what a mixed-content reader sees, so it is content and not layout.
        var (_, result) = Import("<a>\n  <b/>\n</a>");

        var children = Assert.Single(result.Roots).GetChildren(XmlLanguage.ChildrenSlot);

        Assert.Equal(3, children.Count);
        Assert.Equal("\n  ", children[0].GetOrNull(XmlLanguage.TextSlot));
        Assert.Equal(XmlConceptKeys.Element, children[1].Concept.Key);
        Assert.Equal("\n", children[2].GetOrNull(XmlLanguage.TextSlot));
    }

    [Fact]
    public void TextIsStoredInTheSpellingOfTheFile()
    {
        var (_, result) = Import("<a>a &amp; b</a>");

        var text = Assert.Single(Assert.Single(result.Roots).GetChildren(XmlLanguage.ChildrenSlot));

        Assert.Equal("a &amp; b", text.GetOrNull(XmlLanguage.TextSlot));
    }

    [Fact]
    public void AnAttributeValueIsStoredInTheSpellingOfTheFile()
    {
        var (_, result) = Import("<a x=\"a &quot;b&quot; &lt;c&gt;\"/>");

        var attribute = Assert.Single(Assert.Single(result.Roots).GetChildren(XmlLanguage.AttributesSlot));

        Assert.Equal("a &quot;b&quot; &lt;c&gt;", attribute.GetOrNull(XmlLanguage.ValueSlot));
    }

    [Fact]
    public void ANumericReferenceComesBackInItsNamedSpelling()
    {
        // The parser decodes both spellings to one character, and the model has to hold one of them: it holds the
        // canonical one, which is what a corpus of generated files can compare against.
        var (_, result) = Import("<a>&#38;</a>");

        var text = Assert.Single(Assert.Single(result.Roots).GetChildren(XmlLanguage.ChildrenSlot));

        Assert.Equal("&amp;", text.GetOrNull(XmlLanguage.TextSlot));
    }

    [Fact]
    public void ACommentIsANode()
    {
        var (_, result) = Import("<a><!-- note --></a>");

        var comment = Assert.Single(Assert.Single(result.Roots).GetChildren(XmlLanguage.ChildrenSlot));

        Assert.Equal(XmlConceptKeys.Comment, comment.Concept.Key);
        Assert.Equal(" note ", comment.GetOrNull(XmlLanguage.TextSlot));
    }

    [Fact]
    public void ACdataSectionIsKeptAsRawText()
    {
        var (_, result) = Import("<a><![CDATA[x < y & z]]></a>");

        var raw = Assert.Single(Assert.Single(result.Roots).GetChildren(XmlLanguage.ChildrenSlot));

        Assert.Equal(XmlConceptKeys.Raw, raw.Concept.Key);
        Assert.Equal("<![CDATA[x < y & z]]>", raw.GetOrNull(XmlLanguage.TextSlot));
    }

    [Fact]
    public void AProcessingInstructionIsRawAndStaysBeforeTheRoot()
    {
        var (model, result) = Import("<?xml-stylesheet href=\"a\"?><a/>");

        // A processing instruction outside the root element is content of the document, so it becomes a root of its
        // own, in the order the file has it.
        Assert.Equal(2, result.Roots.Count);
        Assert.Equal(2, model.Roots.Count);
        Assert.Equal(XmlConceptKeys.Raw, model.Roots[0].Concept.Key);
        Assert.Equal("<?xml-stylesheet href=\"a\"?>", model.Roots[0].GetOrNull(XmlLanguage.TextSlot));
        Assert.Equal(XmlConceptKeys.Element, model.Roots[1].Concept.Key);
    }

    [Fact]
    public void ACommentBeforeTheRootIsARootOfItsOwn()
    {
        // XML allows content outside the element, so a document can have several top-level nodes - which is why the
        // importer answers a list of roots rather than inventing a container.
        var (model, result) = Import("<!-- leading --><a/>");

        Assert.Equal(2, result.Roots.Count);
        Assert.Equal(2, model.Roots.Count);
        Assert.Equal(XmlConceptKeys.Comment, model.Roots[0].Concept.Key);
        Assert.Equal(XmlConceptKeys.Element, model.Roots[1].Concept.Key);
    }

    [Fact]
    public void TheXmlDeclarationIsNotANode()
    {
        // It says how the bytes a host read were encoded, not what the document contains; a document with one is the
        // document without it.
        var (_, result) = Import("<?xml version=\"1.0\" encoding=\"utf-8\"?><a/>");

        Assert.Equal(XmlConceptKeys.Element, Assert.Single(result.Roots).Concept.Key);
    }

    [Fact]
    public void ANamespaceKeepsThePrefixesTheFileUses()
    {
        var (_, result) = Import("<p:root xmlns:p=\"urn:x\" p:a=\"1\"><p:child/></p:root>");

        var root = Assert.Single(result.Roots);
        var attributes = root.GetChildren(XmlLanguage.AttributesSlot);
        var child = Assert.Single(root.GetChildren(XmlLanguage.ChildrenSlot));

        Assert.Equal("p:root", root.GetOrNull(XmlLanguage.NameSlot));
        Assert.Equal(["xmlns:p", "p:a"], attributes.Select(attribute => attribute.GetOrNull(XmlLanguage.NameSlot)));
        Assert.Equal("p:child", child.GetOrNull(XmlLanguage.NameSlot));
    }

    [Fact]
    public void ADoctypeIsRefusedWithoutReadingIt()
    {
        // The target does not exist, which is the point: nothing may try to open it. DtdProcessing.Prohibit and a null
        // resolver are the guard; resolving a DTD on import is an XXE in every host that embeds this editor.
        var model = XmlLanguage.NewProject().NewModel("xml");
        var before = ModelXml.Serialize(model);

        var result = XmlLanguage.Importer.Import(model, "<!DOCTYPE a SYSTEM \"missing.dtd\"><a/>");

        Assert.False(result.Succeeded);
        Assert.Equal(XmlImporter.ExternalEntityRefused, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(model.Roots);
        Assert.Equal(before, ModelXml.Serialize(model));
    }

    [Fact]
    public void AnUnclosedElementIsRefusedAndNamesItsLine()
    {
        var (model, result) = Import("<a>\n  <b>\n</a>");

        Assert.False(result.Succeeded);
        Assert.Empty(model.Roots);

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(XmlImporter.UnexpectedToken, error.Code);
        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void AnImportIsOneUndoStep()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        using var history = new UndoHistory(model);

        XmlLanguage.Importer.Import(model, "<a><b><c/></b></a>");

        Assert.Equal(1, history.UndoDepth);
        Assert.True(history.Undo());
        Assert.Empty(model.Roots);
    }

    [Fact]
    public void TheFormatIsXml()
    {
        Assert.Equal("xml", XmlLanguage.Importer.Format);
        Assert.True(XmlLanguage.Importer.CanImport("  <a/>"));
        Assert.False(XmlLanguage.Importer.CanImport("{ }"));
        Assert.False(XmlLanguage.Importer.CanImport(""));
    }

    private static (Model Model, ImportResult Result) Import(string xml)
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        return (model, XmlLanguage.Importer.Import(model, xml));
    }
}
