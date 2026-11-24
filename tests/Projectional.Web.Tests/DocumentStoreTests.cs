using Xunit;

namespace Projectional.Web.Tests;

public sealed class DocumentStoreTests
{
    [Fact]
    public void AnIndexSurvivesNamesWithTabsAndNewlines()
    {
        SavedDocument[] documents =
        [
            new("id-1", "Pay roll", "Rules"),
            new("id-2", "a\tb\nc", "Units"),
        ];

        Assert.Equal(documents, DocumentStore.DecodeIndex(DocumentStore.EncodeIndex(documents)));
    }

    [Fact]
    public void AnEmptyStoreIsNoDocumentsRatherThanAnError()
    {
        Assert.Empty(DocumentStore.DecodeIndex(null));
        Assert.Empty(DocumentStore.DecodeIndex(string.Empty));
        Assert.Empty(DocumentStore.EncodeIndex([]));
    }

    [Fact]
    public void ALineThatDoesNotParseIsSkippedAndTheOthersSurvive()
    {
        var index = string.Join(
            '\n',
            "not-a-document",
            "a\tb",
            $"{Uri.EscapeDataString("id")}\t{Uri.EscapeDataString("name")}\t{Uri.EscapeDataString("Rules")}",
            "too\tmany\tfields\there");

        var documents = DocumentStore.DecodeIndex(index);

        var saved = Assert.Single(documents);
        Assert.Equal("id", saved.Id);
        Assert.Equal("name", saved.Name);
        Assert.Equal("Rules", saved.Language);
    }

    [Fact]
    public void EveryDocumentHasItsOwnKeyUnderTheIndex()
    {
        Assert.NotEqual(DocumentStore.IndexKey, DocumentStore.DocumentKey("id"));
        Assert.StartsWith(DocumentStore.DocumentPrefix, DocumentStore.DocumentKey("id"), StringComparison.Ordinal);
    }
}
