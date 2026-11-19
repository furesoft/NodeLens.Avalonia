using Projectional.Core;
using Projectional.Editing;
using Projectional.Toml;
using Projectional.Toml.Intentions;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The three entries a TOML document is built with, and the carets where they mean nothing.</summary>
/// <remarks>
/// A document draws nothing of its own, so the caret is always inside one of its entries - which is why the entries are
/// offered from a pair and not only from the document itself. "Add a table" while editing a pair is exactly the moment a
/// user reaches for it.
/// </remarks>
public sealed class TomlIntentionsTests
{
    private static readonly string[] AllThreeIds =
        [TomlIntentions.AddPairId, TomlIntentions.AddTableId, TomlIntentions.AddArrayOfTablesId];

    [Fact]
    public void APairOffersTheThreeKindsOfEntryItsDocumentHolds()
    {
        var (session, _, _) = Document();

        Assert.Equal(AllThreeIds, IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void AddingAPairAppendsOne()
    {
        var (session, document, _) = Document();

        session.Apply(Offer(session, TomlIntentions.AddPairId));

        var entries = document.GetChildren(TomlLanguage.EntriesSlot);

        Assert.Equal(2, entries.Count);
        Assert.Equal(TomlConceptKeys.KeyValue, entries[1].Concept.Key);
    }

    [Fact]
    public void AddingATableAppendsATableRatherThanCopyingTheLastEntry()
    {
        // The named insertion is the point: after a pair, "add a table" has to create a table.
        var (session, document, _) = Document();

        session.Apply(Offer(session, TomlIntentions.AddTableId));

        Assert.Equal(TomlConceptKeys.Table, document.GetChildren(TomlLanguage.EntriesSlot)[1].Concept.Key);
    }

    [Fact]
    public void AddingAnArrayOfTablesAppendsOne()
    {
        var (session, document, _) = Document();

        session.Apply(Offer(session, TomlIntentions.AddArrayOfTablesId));

        Assert.Equal(
            TomlConceptKeys.ArrayOfTables,
            document.GetChildren(TomlLanguage.EntriesSlot)[1].Concept.Key);
    }

    [Fact]
    public void ATableOffersTheSameThreeEntriesForItsOwnList()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);
        var table = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.Table);
        table.Set(TomlLanguage.KeySlot, "server");
        var pair = model.NewChild(table, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, "port");

        using var session = new EditorSession(document, TomlLanguage.Notations);
        session.AddIntentions(TomlIntentions.Provider, IntentionSource.Language);
        Caret(session, pair);

        Assert.Equal(AllThreeIds, IntentionList.For(session).Select(offer => offer.Id));

        session.Apply(Offer(session, TomlIntentions.AddPairId));

        // The nearest list is the table's, not the document's.
        Assert.Equal(2, table.GetChildren(TomlLanguage.EntriesSlot).Count);
    }

    [Fact]
    public void AnEmptyDocumentOffersTheSameThreeEntries()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);

        using var session = new EditorSession(document, TomlLanguage.Notations);
        session.AddIntentions(TomlIntentions.Provider, IntentionSource.Language);
        Caret(session, document);

        Assert.Equal(AllThreeIds, IntentionList.For(session).Select(offer => offer.Id));
    }

    [Fact]
    public void ANodeWithNoListOfEntriesAroundItOffersNone()
    {
        // A value on its own - no document, no table - has no list to append to, so the entries are absent rather than
        // disabled: an entry that cannot run would be a row a user cannot choose.
        var model = TomlLanguage.NewProject().NewModel("settings");
        var text = model.NewNode(TomlConceptKeys.String);
        text.Set(TomlLanguage.TextSlot, "a");

        using var session = new EditorSession(text, TomlLanguage.Notations);
        session.AddIntentions(TomlIntentions.Provider, IntentionSource.Language);
        Caret(session, text);

        Assert.Empty(IntentionList.For(session));
    }

    /// <summary>A document holding one pair, with the caret inside the pair.</summary>
    private static (EditorSession Session, Node Document, Node Pair) Document()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);
        var pair = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, "a");
        model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Integer)
            .Set(TomlLanguage.TextSlot, "1");

        var session = new EditorSession(document, TomlLanguage.Notations);
        session.AddIntentions(TomlIntentions.Provider, IntentionSource.Language);
        Caret(session, pair);

        return (session, document, pair);
    }

    private static void Caret(EditorSession session, Node node) =>
        session.Select(Selection.At(CaretPosition.At(FirstPlace(session, node), 0)));

    private static CaretPlace FirstPlace(EditorSession session, Node node) =>
        CaretPlaces.Of(session.Tree).First(place => place.Address.Owner == node.Id);

    private static Intention Offer(EditorSession session, string id) =>
        IntentionList.For(session).Single(offer => offer.Id == id);
}
