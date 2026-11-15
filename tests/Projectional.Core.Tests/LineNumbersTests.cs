using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A projectional document has no lines, so a gutter has to decide what counts as one: a band of the
/// layout that text is drawn in, whatever cell put it there.
/// </summary>
/// <remarks>
/// The fixtures are the smallest documents that make the question interesting. <see cref="TestEditor"/>
/// builds them, and <see cref="MonospaceTextMeasure"/> makes one line one unit tall, so a row top can be
/// read off the assertion instead of off a rendering.
/// </remarks>
public sealed class LineNumbersTests
{
    [Fact]
    public void AOneLineNodeIsOneRow()
    {
        var rows = RowsOf(TestEditor.Open(TestEditor.BareStep("walk")));

        var row = Assert.Single(rows);

        Assert.Equal(1, row.Number);
        Assert.Equal(new LayoutRect(0, 0, 0, MonospaceTextMeasure.Instance.LineHeight), row.Bounds);
    }

    [Fact]
    public void ACellThatWrapsIsOneRowPerLine()
    {
        // A break in the value is drawn on two lines, and a reader calls that two rows even though it is
        // one node and one cell.
        var rows = RowsOf(TestEditor.Open(TestEditor.BareStep("a\nb")));

        Assert.Equal([1, 2], rows.Select(row => row.Number));
        Assert.Equal([0, 1], rows.Select(row => row.Bounds.Y));
    }

    [Fact]
    public void CellsThatShareALineShareANumber()
    {
        // The rule's header is six cells - a keyword, three properties and two punctuation marks - and it
        // is one line on the screen, so it is one number.
        var rows = RowsOf(TestEditor.Open(TestEditor.RuleWithStep().Rule));

        Assert.Equal(rows.Count, rows.Select(row => row.Number).Distinct().Count());
        Assert.Equal(rows.Count, rows.Select(row => row.Bounds.Y).Distinct().Count());
    }

    [Fact]
    public void AStackOfLinesIsNumberedDownwardsWithoutGaps()
    {
        var rows = RowsOf(TestEditor.Open(TestEditor.RuleWithStep().Rule));

        Assert.Equal(Enumerable.Range(1, rows.Count), rows.Select(row => row.Number));

        for (var index = 1; index < rows.Count; index++)
        {
            Assert.True(rows[index].Bounds.Y > rows[index - 1].Bounds.Y);
        }
    }

    [Fact]
    public void AnEmptySlotIsARowBecauseItIsVisible()
    {
        // The rule with no body still draws the placeholders of its slots, so it has rows to number - a
        // gutter that skipped them would number a document the reader cannot see.
        var bare = RowsOf(TestEditor.Open(TestEditor.BareRule()));
        var filled = RowsOf(TestEditor.Open(TestEditor.RuleWithStep().Rule));

        Assert.NotEmpty(bare);
        Assert.True(filled.Count >= bare.Count);
    }

    [Fact]
    public void RowsStartAtTheTopOfTheLayout()
    {
        var rows = RowsOf(TestEditor.Open(TestEditor.RuleWithStep().Rule));

        Assert.Equal(0, rows[0].Bounds.Y);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(99, 2)]
    [InlineData(100, 3)]
    [InlineData(1000, 4)]
    public void TheGutterIsAsWideAsTheLongestNumberNeeds(int count, int digits)
    {
        var rows = Enumerable
            .Range(1, count)
            .Select(number => new LineNumberRow(number, default))
            .ToArray();

        Assert.Equal(digits, LineNumbers.DigitsOf(rows));
    }

    [Fact]
    public void NoRowsNeedOneDigitRatherThanNone()
    {
        // A gutter sized from a digit count of zero would have no width at all and the numbers would land
        // on top of the text the moment the document got its first row.
        Assert.Equal(1, LineNumbers.DigitsOf([]));
    }

    private static IReadOnlyList<LineNumberRow> RowsOf(EditorSession session) =>
        LineNumbers.Of(session.Layout, session.Measure);
}
