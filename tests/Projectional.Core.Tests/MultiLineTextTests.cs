using Projectional.Language;
using Projectional.Persistence;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A text cell is not one line: the value may hold line breaks, and a text longer than the room left to
/// the page's edge is drawn on several lines instead of running past it.
/// </summary>
/// <remarks>
/// Everything here is measured by <see cref="MonospaceTextMeasure"/>, so a line's width is counted in
/// characters and can be checked against the text it was cut from. The wrapping rules themselves live in
/// one place - a hard break always ends a line, a soft break is only ever taken at a space, and a word
/// wider than the line is drawn whole, past the edge - and each of them is pinned down here.
/// </remarks>
public sealed class MultiLineTextTests
{
    private static readonly MonospaceTextMeasure Measure = MonospaceTextMeasure.Instance;

    [Fact]
    public void ATextWithoutBreaksIsOneLine()
    {
        var text = Wrap("walk the dog");

        var line = Assert.Single(text.Lines);

        Assert.Equal(0, line.Start);
        Assert.Equal(12, line.Length);
        Assert.Equal(12, line.ContentLength);
        Assert.Equal(12, line.Width);
        Assert.Equal(12, text.Width);
        Assert.Equal(1, text.Height);
        Assert.True(text.IsSingleLine);
    }

    [Fact]
    public void ABreakInTheValueIsAlwaysHonoured()
    {
        // No limit: the value's own breaks are the only thing that ends a line.
        var text = Wrap("a\nb");

        Assert.Equal(2, text.LineCount);
        Assert.Equal(new TextLine(0, 2, 1, 1), text.Lines[0]);
        Assert.Equal(new TextLine(2, 1, 1, 1), text.Lines[1]);
        Assert.Equal(1, text.Width);
        Assert.Equal(2, text.Height);
    }

    [Fact]
    public void AnEmptyLineInTheValueIsALineOfItsOwn()
    {
        var text = Wrap("a\n\nb");

        Assert.Equal(3, text.LineCount);
        Assert.Equal(new TextLine(2, 1, 0, 0), text.Lines[1]);
        Assert.True(text.Lines[1].IsEmpty);
        Assert.Equal(3, text.Height);
    }

    [Fact]
    public void AValueEndingInABreakEndsWithAnEmptyLine()
    {
        // The caret has to be able to stand on the line the user just opened.
        var text = Wrap("a\n");

        Assert.Equal(2, text.LineCount);
        Assert.Equal(new TextLine(0, 2, 1, 1), text.Lines[0]);
        Assert.Equal(new TextLine(2, 0, 0, 0), text.Lines[1]);
    }

    [Fact]
    public void AnEmptyTextIsOneEmptyLineAsWideAsOneSpace()
    {
        var text = Wrap(string.Empty);

        Assert.Equal(new TextLine(0, 0, 0, 0), Assert.Single(text.Lines));
        Assert.Equal(1, text.Width);
    }

    [Fact]
    public void ATextWiderThanTheLimitIsBrokenAtASpace()
    {
        var text = Wrap("hello world", limit: 5);

        Assert.Equal(2, text.LineCount);
        Assert.Equal(new TextLine(0, 6, 5, 5), text.Lines[0]);
        Assert.Equal(new TextLine(6, 5, 5, 5), text.Lines[1]);

        // The space is consumed but not drawn, so the lines cover the value without overlapping and
        // the caret never stands on a break.
        Assert.Equal(6, text.Lines[0].End);
        Assert.Equal(5, text.Lines[0].ContentEnd);
    }

    [Fact]
    public void AWordThatFitsIsNotBrokenEvenWhenTheLineWouldHaveHadRoom()
    {
        var text = Wrap("aa bbb", limit: 5);

        // Breaking after "aa" rather than inside "bbb" is the point of wrapping at spaces.
        Assert.Equal(2, text.LineCount);
        Assert.Equal("aa", Content(text, 0));
        Assert.Equal("bbb", Content(text, 1));
    }

    [Fact]
    public void AWordWiderThanTheLineRunsPastTheEdgeInsteadOfBeingBroken()
    {
        var text = Wrap("abcdefg", limit: 3);

        // Breaking it would give one character per line, which turns a one-row cell into a cell of
        // seven rows and makes the gutter count a document it does not have. The word is drawn whole
        // and the reader scrolls to it.
        var line = Assert.Single(text.Lines);

        Assert.Equal("abcdefg", Content(text, 0));
        Assert.Equal(7, line.Width);
        Assert.Equal(7, text.Width);
        Assert.Equal(1, text.Height);
    }

    [Fact]
    public void AWordWithAlmostNoRoomLeftIsStillDrawnWhole()
    {
        // This is the shape of the bug: the last token of a line starts just inside the page's edge,
        // so the room left holds about one character.
        var text = Wrap("Accept", limit: 1);

        var line = Assert.Single(text.Lines);

        Assert.Equal("Accept", Content(text, 0));
        Assert.Equal(6, line.Width);
    }

    [Fact]
    public void ABreakAndAWrapCanMeetInOneValue()
    {
        var text = Wrap("hello world\nbye", limit: 5);

        Assert.Equal(3, text.LineCount);
        Assert.Equal("hello", Content(text, 0));
        Assert.Equal("world", Content(text, 1));
        Assert.Equal("bye", Content(text, 2));
        Assert.Equal(3, text.Height);
    }

    [Fact]
    public void TheOffsetToLineMappingFollowsTheLines()
    {
        var text = Wrap("hello world", limit: 5);

        // The first line holds offsets 0 to 5: the break at 5 is not drawn, so it is still a caret
        // position on the line it ends, and the second line starts at 6.
        Assert.Equal(0, text.IndexAt(0));
        Assert.Equal(0, text.IndexAt(5));
        Assert.Equal(1, text.IndexAt(6));
        Assert.Equal(1, text.IndexAt(11));

        Assert.Equal(3, text.ColumnOf(3));
        Assert.Equal(0, text.ColumnOf(6));
        Assert.Equal(3, text.ColumnOf(9));
        Assert.Equal(5, text.ColumnOf(11));

        Assert.Equal(6, text.OffsetAt(1, 0));
        Assert.Equal(9, text.OffsetAt(1, 3));
        Assert.Equal(11, text.OffsetAt(1, 99));

        // Every offset of the value maps to exactly one place on exactly one line and back.
        for (var offset = 0; offset <= 11; offset++)
        {
            Assert.Equal(offset, text.OffsetAt(text.IndexAt(offset), text.ColumnOf(offset)));
        }
    }

    [Fact]
    public void ALongerTextMakesItsCellTallerAndPushesTheRestOntoItsLastLine()
    {
        // "rule " is five wide, so the name has seven characters of room before the page's edge. Its
        // first word is eight wide and does not fit, so the wrap is taken at the space after it.
        var layouter = new CellLayouter(Measure, wrapWidth: 12);
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        rule.Set("name", "abcdefgh ijkl");
        var tree = new EditorTreeBuilder(TestNotations.Registry).Build(rule);
        var line = tree.Root.Children[0];

        var layout = layouter.Layout(tree);
        var name = line.Children[2];

        var lines = layout.LinesOf(name);

        Assert.NotNull(lines);
        Assert.Equal(2, lines.LineCount);
        Assert.Equal(2, layout.BoundsOf(name)!.Value.Height);

        // What follows the name - its space and then "as" - continues on the name's last line.
        Assert.Equal(1, layout.BoundsOf(line.Children[4])!.Value.Y);
    }

    [Fact]
    public void APageWithNoEdgeNeverWrapsSoftly()
    {
        var layouter = new CellLayouter(Measure);
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        rule.Set("name", "a rather long name that would not fit on a narrow page");
        var tree = new EditorTreeBuilder(TestNotations.Registry).Build(rule);

        var layout = layouter.Layout(tree);
        var name = tree.Root.Children[0].Children[2];

        // Without an edge to wrap at the value stays one line, however long it is.
        Assert.Single(layout.LinesOf(name)!.Lines);
        Assert.Equal(1, layout.BoundsOf(name)!.Value.Height);
    }

    [Fact]
    public void AValueWithALineBreakSurvivesBeingSaved()
    {
        var project = TestLanguage.NewProject();
        var model = project.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        rule.Set("name", "first\nsecond");

        var stored = ModelXml.Serialize(model);
        var reloaded = ModelXml.Deserialize(TestLanguage.NewProject(), stored);

        Assert.Equal("first\nsecond", reloaded.Roots[0].Get("name"));
        Assert.Equal(stored, ModelXml.Serialize(reloaded));
    }

    private static LaidOutText Wrap(string text, double? limit = null) =>
        LaidOutText.Wrap(text, TextEmphasis.None, Measure, limit);

    private static string Content(LaidOutText text, int index) => text.ContentOf(index);
}
