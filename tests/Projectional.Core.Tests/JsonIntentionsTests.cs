using Projectional.Core;
using Projectional.Editing;
using Projectional.Json;
using Projectional.Json.Intentions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The two entries a JSON document is built with, and the carets where they mean nothing.</summary>
public sealed class JsonIntentionsTests
{
    [Fact]
    public void AnObjectOffersOneMoreMember()
    {
        var (session, document) = Document();

        Assert.Equal([JsonIntentions.AddMemberId], IntentionList.For(session).Select(offer => offer.Id));

        session.Apply(Offer(session, JsonIntentions.AddMemberId));

        var members = document.GetChildren(JsonLanguage.MembersSlot);

        Assert.Equal(2, members.Count);
        Assert.Equal(JsonConceptKeys.Property, members[1].Concept.Key);
    }

    [Fact]
    public void ASecondMemberGoesAfterTheFirst()
    {
        // The empty case fills the slot; the filled case appends, because that is what "one more of these" means.
        var (session, document) = Document();
        var first = document.GetChildren(JsonLanguage.MembersSlot)[0];

        session.Apply(Offer(session, JsonIntentions.AddMemberId));
        session.Select(Selection.At(CaretPosition.At(FirstPlace(session, document), 0)));
        session.Apply(Offer(session, JsonIntentions.AddMemberId));

        var members = document.GetChildren(JsonLanguage.MembersSlot);

        Assert.Equal(3, members.Count);
        Assert.Same(first, members[0]);
    }

    [Fact]
    public void AnArrayOffersOneMoreItemRatherThanOneMoreMember()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var array = model.NewNode(JsonConceptKeys.Array);
        using var session = new EditorSession(array, JsonLanguage.Notations);
        session.AddIntentions(JsonIntentions.Provider, IntentionSource.Language);

        Caret(session, array);

        Assert.Equal([JsonIntentions.AddItemId], IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void AScalarOffersNeither()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var text = model.NewNode(JsonConceptKeys.Text);
        text.Set(JsonLanguage.TextSlot, "a");
        using var session = new EditorSession(text, JsonLanguage.Notations);
        session.AddIntentions(JsonIntentions.Provider, IntentionSource.Language);

        Caret(session, text);

        Assert.Empty(IntentionList.For(session));
    }

    /// <summary>A document holding one object with a single member.</summary>
    private static (EditorSession Session, Node Document) Document()
    {
        var model = JsonLanguage.NewProject().NewModel("json");
        var document = model.NewNode(JsonConceptKeys.Object);
        var member = model.NewChild(document, JsonLanguage.MembersSlot, JsonConceptKeys.Property);
        member.Set(JsonLanguage.NameSlot, "a");
        model.NewChild(member, JsonLanguage.ValueSlot, JsonConceptKeys.Null);

        var session = new EditorSession(document, JsonLanguage.Notations);
        session.AddIntentions(JsonIntentions.Provider, IntentionSource.Language);
        Caret(session, document);

        return (session, document);
    }

    private static void Caret(EditorSession session, Node node) =>
        session.Select(Selection.At(CaretPosition.At(FirstPlace(session, node), 0)));

    private static CaretPlace FirstPlace(EditorSession session, Node node) =>
        CaretPlaces.Of(session.Tree).First(place => place.Address.Owner == node.Id);

    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);
}
