using Projectional.Core;
using Projectional.Editing;
using Projectional.Toml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A node of a named concept inserted into a list slot, and the cases the model would refuse.</summary>
/// <remarks>
/// The action exists because a list can hold several kinds of entry: <c>InsertSiblingAction</c> copies the concept of the
/// node it stands beside and <c>AddChildAction</c> fills only an empty slot, so neither can say "append a table".
/// </remarks>
public sealed class InsertNamedNodeActionTests
{
    [Fact]
    public void TheNamedConceptIsAppendedAfterTheLastEntry()
    {
        var (session, document) = Document();

        var result = session.Apply(new InsertNamedNodeAction(
            document.Id,
            TomlLanguage.EntriesSlot,
            index: 1,
            TomlConceptKeys.Table));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var entries = document.GetChildren(TomlLanguage.EntriesSlot);

        Assert.Equal(2, entries.Count);
        Assert.Equal(TomlConceptKeys.Table, entries[1].Concept.Key);
    }

    [Fact]
    public void AConceptTheSlotDoesNotHoldIsRejectedWithAReason()
    {
        var (session, document) = Document();

        var result = session.Apply(new InsertNamedNodeAction(
            document.Id,
            TomlLanguage.EntriesSlot,
            index: 1,
            TomlConceptKeys.Integer));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Single(document.GetChildren(TomlLanguage.EntriesSlot));
    }

    [Fact]
    public void AFilledSingleValuedSlotIsRejectedRatherThanReplaced()
    {
        var (session, document) = Document();
        var pair = document.GetChildren(TomlLanguage.EntriesSlot)[0];

        var result = session.Apply(new InsertNamedNodeAction(
            pair.Id,
            TomlLanguage.ValueSlot,
            index: 0,
            TomlConceptKeys.Bool));

        Assert.Equal(EditOutcome.Rejected, result.Outcome);
        Assert.Single(pair.GetChildren(TomlLanguage.ValueSlot));
    }

    /// <summary>A document holding one pair whose value is set, with a session over it.</summary>
    private static (EditorSession Session, Node Document) Document()
    {
        var model = TomlLanguage.NewProject().NewModel("settings");
        var document = model.NewNode(TomlConceptKeys.Document);
        var pair = model.NewChild(document, TomlLanguage.EntriesSlot, TomlConceptKeys.KeyValue);
        pair.Set(TomlLanguage.KeySlot, "a");
        model.NewChild(pair, TomlLanguage.ValueSlot, TomlConceptKeys.Integer)
            .Set(TomlLanguage.TextSlot, "1");

        return (new EditorSession(document, TomlLanguage.Notations), document);
    }
}
