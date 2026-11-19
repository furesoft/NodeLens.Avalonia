using Projectional.Core;
using Projectional.Editing;
using Projectional.Xml;
using Projectional.Xml.Checks;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The rules XML states about itself, and the repair each finding carries.</summary>
public sealed class XmlChecksTests
{
    [Fact]
    public void ATextWithABareAmpersandIsReportedAndEscaped()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        var text = model.NewNode(XmlConceptKeys.Text);
        text.Set(XmlLanguage.TextSlot, "a & b");

        var finding = Assert.Single(new XmlSpellingCheck().Find(model));

        Assert.Equal(XmlSpellingCheck.Code, finding.Diagnostic.Code);

        using var session = new EditorSession(text, XmlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("a &amp; b", text.GetOrNull(XmlLanguage.TextSlot));
        Assert.Empty(new XmlSpellingCheck().Find(model));
    }

    [Fact]
    public void ATabInAnAttributeValueIsReportedAndSpelledAsAReference()
    {
        // A literal tab would be normalised to a space on the way back in, so the value would change quietly.
        var model = XmlLanguage.NewProject().NewModel("xml");
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");
        var attribute = model.NewChild(element, XmlLanguage.AttributesSlot, XmlConceptKeys.Attribute);
        attribute.Set(XmlLanguage.NameSlot, "x");
        attribute.Set(XmlLanguage.ValueSlot, "one\ttwo");

        var finding = Assert.Single(new XmlSpellingCheck().Find(model));

        using var session = new EditorSession(element, XmlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("one&#x9;two", attribute.GetOrNull(XmlLanguage.ValueSlot));
    }

    [Fact]
    public void ACanonicalValueIsLeftAlone()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        model.NewNode(XmlConceptKeys.Text).Set(XmlLanguage.TextSlot, "a &amp; b");
        model.NewNode(XmlConceptKeys.Text).Set(XmlLanguage.TextSlot, "\n  ");

        Assert.Empty(new XmlSpellingCheck().Find(model));
    }

    [Fact]
    public void ACommentWithTwoDashesIsReportedAndSeparated()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        var comment = model.NewNode(XmlConceptKeys.Comment);
        comment.Set(XmlLanguage.TextSlot, "a -- b");

        var finding = Assert.Single(new XmlCommentCheck().Find(model));

        Assert.Equal(XmlCommentCheck.Code, finding.Diagnostic.Code);

        using var session = new EditorSession(comment, XmlLanguage.Notations);
        session.Fixes = new FixSet(finding);

        Assert.Equal(EditOutcome.Applied, session.Apply(finding).Outcome);
        Assert.Equal("a - - b", comment.GetOrNull(XmlLanguage.TextSlot));
        Assert.Empty(new XmlCommentCheck().Find(model));
    }

    [Fact]
    public void AWellFormedDocumentIsClean()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        model.NewNode(XmlConceptKeys.Text).Set(XmlLanguage.TextSlot, "a &lt; b");

        Assert.Empty(XmlChecks.All.Find(model));
    }
}
