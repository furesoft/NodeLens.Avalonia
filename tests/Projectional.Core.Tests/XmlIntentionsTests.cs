using Projectional.Core;
using Projectional.Editing;
using Projectional.Xml;
using Projectional.Xml.Intentions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The two entries XML owes its notation, which are the only way into an empty element.</summary>
public sealed class XmlIntentionsTests
{
    [Fact]
    public void AnElementOffersBothOfItsSlots()
    {
        var (session, _) = Document();

        Assert.Equal(
            [XmlIntentions.AddAttributeId, XmlIntentions.AddChildId],
            IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void AddingAnAttributeFillsTheSlotTheNotationCannotDraw()
    {
        // The notation hides an empty attributes collection, so without this entry an element could never gain its
        // first attribute.
        var (session, element) = Document();

        session.Apply(Offer(session, XmlIntentions.AddAttributeId));

        var attribute = Assert.Single(element.GetChildren(XmlLanguage.AttributesSlot));

        Assert.Equal(XmlConceptKeys.Attribute, attribute.Concept.Key);
    }

    [Fact]
    public void AddingAChildFillsTheContentSlot()
    {
        var (session, element) = Document();

        session.Apply(Offer(session, XmlIntentions.AddChildId));

        var child = Assert.Single(element.GetChildren(XmlLanguage.ChildrenSlot));

        Assert.Equal(XmlConceptKeys.Element, child.Concept.Key);
    }

    [Fact]
    public void ASecondEntryAppendsAfterTheFirst()
    {
        var (session, element) = Document();

        session.Apply(Offer(session, XmlIntentions.AddChildId));
        var first = element.GetChildren(XmlLanguage.ChildrenSlot)[0];

        Caret(session, element);
        session.Apply(Offer(session, XmlIntentions.AddChildId));

        var children = element.GetChildren(XmlLanguage.ChildrenSlot);

        Assert.Equal(2, children.Count);
        Assert.Same(first, children[0]);
    }

    [Fact]
    public void AnAttributeOffersNothing()
    {
        // The entries are about the element; an attribute is a leaf and has no slot to grow.
        var (session, element) = Document();

        session.Apply(Offer(session, XmlIntentions.AddAttributeId));

        var attribute = Assert.Single(element.GetChildren(XmlLanguage.AttributesSlot));

        Caret(session, attribute);

        Assert.Empty(IntentionList.For(session));
    }

    private static (EditorSession Session, Node Element) Document()
    {
        var model = XmlLanguage.NewProject().NewModel("xml");
        var element = model.NewNode(XmlConceptKeys.Element);
        element.Set(XmlLanguage.NameSlot, "a");

        var session = new EditorSession(element, XmlLanguage.Notations);
        session.AddIntentions(XmlIntentions.Provider, IntentionSource.Language);
        Caret(session, element);

        return (session, element);
    }

    private static void Caret(EditorSession session, Node node) =>
        session.Select(Selection.At(CaretPosition.At(
            CaretPlaces.Of(session.Tree).First(place => place.Address.Owner == node.Id),
            0)));

    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);
}
