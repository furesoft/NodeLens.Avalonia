using Projectional.Core;
using Projectional.Editing;
using Projectional.Hocon;
using Projectional.Hocon.Intentions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The two entries a HOCON document is built with, and the carets where they mean nothing.</summary>
public sealed class HoconIntentionsTests
{
    [Fact]
    public void APropertyOffersOneMoreProperty()
    {
        var (session, document) = Document();

        Assert.Equal([HoconIntentions.AddPropertyId], IntentionList.For(session).Select(offer => offer.Id));

        session.Apply(Offer(session, HoconIntentions.AddPropertyId));

        Assert.Equal(2, document.GetChildren(HoconLanguage.MembersSlot).Count);
    }

    [Fact]
    public void AnObjectOffersOneMorePropertyForItsOwnList()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var document = model.NewNode(HoconConceptKeys.Document);
        var property = model.NewChild(document, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        property.Set(HoconLanguage.KeySlot, "server");
        property.Set(HoconLanguage.OperatorSlot, HoconLanguage.SpaceOperator);
        var server = model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Object);
        var host = model.NewChild(server, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        host.Set(HoconLanguage.KeySlot, "host");

        using var session = new EditorSession(document, HoconLanguage.Notations);
        session.AddIntentions(HoconIntentions.Provider, IntentionSource.Language);
        Caret(session, host);

        session.Apply(Offer(session, HoconIntentions.AddPropertyId));

        // The nearest list is the object's, not the document's.
        Assert.Equal(2, server.GetChildren(HoconLanguage.MembersSlot).Count);
        Assert.Single(document.GetChildren(HoconLanguage.MembersSlot));
    }

    [Fact]
    public void AnArrayOffersOneMoreItem()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var array = model.NewNode(HoconConceptKeys.Array);
        var item = model.NewChild(array, HoconLanguage.ItemsSlot, HoconConceptKeys.Bool);
        item.Set(HoconLanguage.ValueSlot, "true");

        using var session = new EditorSession(array, HoconLanguage.Notations);
        session.AddIntentions(HoconIntentions.Provider, IntentionSource.Language);
        Caret(session, item);

        Assert.Equal([HoconIntentions.AddItemId], IntentionList.For(session).Select(offer => offer.Id));

        session.Apply(Offer(session, HoconIntentions.AddItemId));

        Assert.Equal(2, array.GetChildren(HoconLanguage.ItemsSlot).Count);
    }

    [Fact]
    public void AStandaloneValueOffersNothing()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var text = model.NewNode(HoconConceptKeys.String);
        text.Set(HoconLanguage.TextSlot, "a");

        using var session = new EditorSession(text, HoconLanguage.Notations);
        session.AddIntentions(HoconIntentions.Provider, IntentionSource.Language);
        Caret(session, text);

        Assert.Empty(IntentionList.For(session));
    }

    /// <summary>A document holding one property, with the caret inside it.</summary>
    private static (EditorSession Session, Node Document) Document()
    {
        var model = HoconLanguage.NewProject().NewModel("application");
        var document = model.NewNode(HoconConceptKeys.Document);
        var property = model.NewChild(document, HoconLanguage.MembersSlot, HoconConceptKeys.Property);
        property.Set(HoconLanguage.KeySlot, "a");
        model.NewChild(property, HoconLanguage.ValueSlot, HoconConceptKeys.Number)
            .Set(HoconLanguage.LiteralSlot, "1");

        var session = new EditorSession(document, HoconLanguage.Notations);
        session.AddIntentions(HoconIntentions.Provider, IntentionSource.Language);
        Caret(session, property);

        return (session, document);
    }

    private static void Caret(EditorSession session, Node node) =>
        session.Select(Selection.At(CaretPosition.At(FirstPlace(session, node), 0)));

    private static CaretPlace FirstPlace(EditorSession session, Node node) =>
        CaretPlaces.Of(session.Tree).First(place => place.Address.Owner == node.Id);

    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);
}
