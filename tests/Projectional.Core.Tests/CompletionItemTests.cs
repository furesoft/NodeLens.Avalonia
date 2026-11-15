using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The two payloads a proposal can carry: a concept key, whose label is derived from the concept, and a
/// value, whose label is the text that will be written.
/// </summary>
public sealed class CompletionItemTests
{
    [Fact]
    public void AValueIsLabelledWithItsSymbolVerbatim()
    {
        // A concept key is read as `Language.Concept` and shortened, but an enumeration symbol is the
        // value itself - so a symbol that happens to contain a dot must not be treated as a key.
        Assert.Equal("Rules.Rule", CompletionItem.ForValue("Rules.Rule").Label);
        Assert.Equal("Review", CompletionItem.ForValue("Review").Label);
    }

    [Fact]
    public void AValueItemKnowsWhatKindItIs()
    {
        var item = CompletionItem.ForValue("Review");

        Assert.Equal(CompletionKind.Value, item.Kind);
        Assert.Equal("Review", item.ConceptKey);
    }
}
