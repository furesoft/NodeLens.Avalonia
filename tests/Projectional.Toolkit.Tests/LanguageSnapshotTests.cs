using Projectional.Core;
using Projectional.Projection;
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

    [Fact]
    public void ARunSaysWhereItsTextIsAndWhatItMeans()
    {
        var model = Board("Shopping", "milk");

        // The same drawing as text, answered as pieces: a caller that colours a picture needs the position the renderer
        // chose and the classification the editor paints with, and re-deriving either from the characters is how a
        // highlighter drifts from the editor it documents.
        var runs = LanguageSnapshot.Runs(model, BoardNotations.Registry, width: 40);

        Assert.Equal("board", Assert.Single(runs, run => run.Text == "board").Text);
        Assert.Contains(runs, run => run.Text == "board" && run.Kind == TextStyleKind.Keyword && run.Row == 0 && run.Column == 0);
        Assert.Contains(runs, run => run.Text == "Shopping" && run.Kind == TextStyleKind.Text && run.Row == 0 && run.Column == 6);
        Assert.Contains(runs, run => run.Text == "milk" && run.Kind == TextStyleKind.Text && run.Row == 1 && run.Column == 4);

        // The pieces still spell the text the renderer would have drawn, which is what stops the two from disagreeing.
        var characters = LanguageSnapshot.Render(model, BoardNotations.Registry, width: 40);

        Assert.All(runs, run => Assert.InRange(run.Row, 0, characters.Split('\n').Length - 1));
        Assert.Equal("board", characters.Split('\n')[0][..5]);
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