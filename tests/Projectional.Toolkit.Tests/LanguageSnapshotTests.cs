using Projectional.Core;
using Projectional.Persistence;
using Xunit;

namespace Projectional.Toolkit.Tests;

/// <summary>
/// What a language author can do with one reference: declare a language, draw it, and store it.
/// </summary>
/// <remarks>
/// This project references <c>Projectional.Toolkit</c> and nothing else. Every assertion below therefore doubles as a
/// statement about the package: if the drawing needed the projection layer's internals, or the round trip needed a
/// second reference, this file would not compile - which is cheaper to maintain than a rule in a document.
/// </remarks>
public sealed class LanguageSnapshotTests
{
    [Fact]
    public void ADeclaredLanguageDrawsItself()
    {
        var model = Board("Shopping", "milk", "bread");

        var drawing = LanguageSnapshot.Render(model, BoardNotations.Registry, width: 40);

        Assert.Equal(
            string.Join(
                '\n',
                "board Shopping",
                "  - milk",
                "  - bread"),
            drawing);
    }

    [Fact]
    public void TheWidthIsACutAndNotAWrap()
    {
        // A snapshot clips what the caller's width cannot hold, because the caller asked for that width: wrapping
        // would answer a question nobody put.
        var model = Board("Shopping", "a very long item that cannot fit");

        var drawing = LanguageSnapshot.Render(model, BoardNotations.Registry, width: 12);

        Assert.DoesNotContain("cannot fit", drawing, StringComparison.Ordinal);
        Assert.All(drawing.Split('\n'), line => Assert.True(line.Length <= 12, $"'{line}' is wider than 12."));
    }

    [Fact]
    public void OneNodeCanBeDrawnOnItsOwn()
    {
        var model = Board("Shopping", "milk");
        var item = model.Roots[0].GetChildren(BoardLanguage.ItemsSlot)[0];

        var drawing = LanguageSnapshot.Render(item, BoardNotations.Registry, width: 20);

        Assert.Equal("- milk", drawing);
    }

    [Fact]
    public void AStoredModelComesBackByteForByte()
    {
        var model = Board("Shopping", "milk", "bread");

        var first = ModelXml.Serialize(model);
        var reloaded = ModelXml.Deserialize(BoardLanguage.NewProject(), first);

        Assert.Equal(first, ModelXml.Serialize(reloaded));
        Assert.Equal(BoardLanguage.Board, Assert.Single(reloaded.Roots).Concept.Key);
        Assert.Same(
            BoardLanguage.Concepts.Get(BoardLanguage.Item),
            reloaded.Roots[0].GetChildren(BoardLanguage.ItemsSlot)[0].Concept);
    }

    /// <summary>A board with one item per entry of <paramref name="texts"/>.</summary>
    private static Model Board(string title, params string[] texts)
    {
        var model = BoardLanguage.NewProject().NewModel("board");
        var board = model.NewNode(BoardLanguage.Board);

        board.Set("title", title);

        foreach (var text in texts)
        {
            var item = model.NewChild(board, BoardLanguage.ItemsSlot, BoardLanguage.Item);
            item.Set("text", text);
        }

        return model;
    }
}