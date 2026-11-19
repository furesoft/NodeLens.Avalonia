using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A horizontal cell puts its blocks side by side instead of one below the other.
/// </summary>
/// <remarks>
/// It is the one cell that decides a direction a notation could not express otherwise: a sequence stacks and a line
/// holds inline cells, so "a block beside a block" had nowhere to be written. The interesting half is the layout -
/// blocks of different heights still read as one row, because the run continues on the last line of the taller one
/// (hanging continuation), which is exactly what a closing marker after a group of cells needs.
/// </remarks>
public sealed class HorizontalNotationTests
{
    private const string Rule = "C.Rule";

    [Fact]
    public void TwoBlocksOfOneLineStandOnTheSameRow()
    {
        // No gap is implied: a horizontal cell arranges what it is given, and where a blank belongs is the
        // notation's business - the same rule a collection's separator follows.
        var drawn = Render(ed => ed.Horizontal(
            ed.Line(ed.Keyword("x")),
            ed.Line(ed.Keyword("y"))));

        Assert.Equal("xy", drawn.Trim());
    }

    [Fact]
    public void AGapBetweenThemIsWrittenAsACellOfItsOwn()
    {
        var drawn = Render(ed => ed.Horizontal(
            ed.Line(ed.Keyword("x")),
            ed.Line(ed.Space),
            ed.Line(ed.Keyword("y"))));

        Assert.Equal("x y", drawn.Trim());
    }

    [Fact]
    public void ATallerBlockKeepsTheRowAndTheNextOneHangsOnItsLastLine()
    {
        // Hanging continuation, the same rule a line follows for its inline cells: the row is as tall as its tallest
        // block, and the block after it starts on that block's last line rather than at its top.
        var drawn = Render(ed => ed.Horizontal(
            ed.Sequence(ed.Line(ed.Keyword("b")), ed.Line(ed.Keyword("c"))),
            ed.Line(ed.Keyword("d"))));

        Assert.Equal(string.Join('\n', "b", "cd"), drawn);
    }

    [Fact]
    public void TheRowSitsWhereTheSequenceAroundItPutIt()
    {
        // A horizontal cell is a block like any other, so what follows it starts on the next line - the row does not
        // leak into its siblings.
        var drawn = Render(ed => ed.Sequence(
            ed.Horizontal(
                ed.Sequence(ed.Line(ed.Keyword("b")), ed.Line(ed.Keyword("c"))),
                ed.Line(ed.Keyword("d"))),
            ed.Line(ed.Keyword("e"))));

        Assert.Equal(string.Join('\n', "b", "cd", "e"), drawn);
    }

    [Fact]
    public void AnEmptyHorizontalCellDrawsNothing()
    {
        var drawn = Render(ed => ed.Sequence(
            ed.Horizontal(),
            ed.Line(ed.Keyword("after"))));

        Assert.Equal("after", drawn.Trim());
    }

    private static string Render(Func<NotationEditor, BlockNotationCell> build)
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration(
                Rule,
                [new PropertySlot("name", PropertyType.String)]),
        ]);

        var notations = new NotationRegistry(concepts);

        notations.Add(Rule, build);

        var model = new Project(concepts).NewModel();

        return LayoutSnapshot.Render(model.NewNode(Rule), notations, width: 40);
    }
}
