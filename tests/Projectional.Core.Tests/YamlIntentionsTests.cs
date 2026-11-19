using Projectional.Core;
using Projectional.Editing;
using Projectional.Yaml;
using Projectional.Yaml.Intentions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The two entries a YAML document is built with, and the carets where they mean nothing.</summary>
public sealed class YamlIntentionsTests
{
    [Fact]
    public void AMappingOffersOneMoreMember()
    {
        var (session, mapping) = Mapping();

        Assert.Equal([YamlIntentions.AddMemberId], IntentionList.For(session).Select(offer => offer.Id));

        session.Apply(Offer(session, YamlIntentions.AddMemberId));

        Assert.Equal(2, mapping.GetChildren(YamlLanguage.MembersSlot).Count);
    }

    [Fact]
    public void ASequenceOffersOneMoreItem()
    {
        var model = NewModel();
        var sequence = model.NewNode(YamlConceptKeys.Sequence);
        var item = model.NewChild(sequence, YamlLanguage.ItemsSlot, YamlConceptKeys.SequenceItem);
        model.NewChild(item, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "one");

        using var session = new EditorSession(sequence, YamlLanguage.Notations);
        session.AddIntentions(YamlIntentions.Provider, IntentionSource.Language);
        Caret(session, item);

        Assert.Equal([YamlIntentions.AddItemId], IntentionList.For(session).Select(offer => offer.Id));

        session.Apply(Offer(session, YamlIntentions.AddItemId));

        Assert.Equal(2, sequence.GetChildren(YamlLanguage.ItemsSlot).Count);
    }

    [Fact]
    public void TheNearestListIsTheOneTheCaretIsIn()
    {
        var model = NewModel();
        var document = model.NewNode(YamlConceptKeys.Document);
        var outer = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Mapping);
        var pair = model.NewChild(outer, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "server");
        var inner = model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Mapping);
        var port = model.NewChild(inner, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);
        model.NewChild(port, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "port");

        using var session = new EditorSession(document, YamlLanguage.Notations);
        session.AddIntentions(YamlIntentions.Provider, IntentionSource.Language);
        Caret(session, port);

        session.Apply(Offer(session, YamlIntentions.AddMemberId));

        // The nested mapping gains the member, not the document: a pair is always within the mapping it belongs to.
        Assert.Equal(2, inner.GetChildren(YamlLanguage.MembersSlot).Count);
        Assert.Single(outer.GetChildren(YamlLanguage.MembersSlot));
    }

    [Fact]
    public void AFlowCollectionOffersNothing()
    {
        // Its slots have the same names but hold flow members, so appending a block pair would produce a document no reader
        // accepts - which is why only a block collection answers.
        var model = NewModel();
        var mapping = model.NewNode(YamlConceptKeys.FlowMapping);
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.FlowPair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");

        using var session = new EditorSession(mapping, YamlLanguage.Notations);
        session.AddIntentions(YamlIntentions.Provider, IntentionSource.Language);
        Caret(session, pair);

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void ADocumentItselfOffersNothing()
    {
        // Its list holds entries, and a pair is no entry - so there is no member to add at the document.
        var model = NewModel();
        var document = model.NewNode(YamlConceptKeys.Document);
        var note = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Comment);
        note.Set(YamlLanguage.TextSlot, " a note");

        using var session = new EditorSession(document, YamlLanguage.Notations);
        session.AddIntentions(YamlIntentions.Provider, IntentionSource.Language);
        Caret(session, note);

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void ALooseScalarOffersNothing()
    {
        var model = NewModel();
        var scalar = model.NewNode(YamlConceptKeys.Scalar);
        scalar.Set(YamlLanguage.TextSlot, "a");

        using var session = new EditorSession(scalar, YamlLanguage.Notations);
        session.AddIntentions(YamlIntentions.Provider, IntentionSource.Language);
        Caret(session, scalar);

        Assert.Empty(IntentionList.For(session));
    }

    /// <summary>A document holding one mapping with one pair, with the caret inside the pair.</summary>
    private static (EditorSession Session, Node Mapping) Mapping()
    {
        var model = NewModel();
        var document = model.NewNode(YamlConceptKeys.Document);
        var mapping = model.NewChild(document, YamlLanguage.EntriesSlot, YamlConceptKeys.Mapping);
        var pair = model.NewChild(mapping, YamlLanguage.MembersSlot, YamlConceptKeys.Pair);
        model.NewChild(pair, YamlLanguage.KeySlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "a");
        model.NewChild(pair, YamlLanguage.ValueSlot, YamlConceptKeys.Scalar).Set(YamlLanguage.TextSlot, "1");

        var session = new EditorSession(document, YamlLanguage.Notations);
        session.AddIntentions(YamlIntentions.Provider, IntentionSource.Language);
        Caret(session, pair);

        return (session, mapping);
    }

    private static void Caret(EditorSession session, Node node) =>
        session.Select(Selection.At(CaretPosition.At(FirstPlace(session, node), 0)));

    private static CaretPlace FirstPlace(EditorSession session, Node node) =>
        CaretPlaces.Of(session.Tree).First(place => place.Address.Owner == node.Id);

    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);

    private static Model NewModel() => YamlLanguage.NewProject().NewModel("pipeline");
}
