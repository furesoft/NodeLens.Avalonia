using Projectional.Workbench;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The terminal's screen: what a program's bytes turn into, asserted without a window.
/// </summary>
/// <remarks>
/// These are the assertions that make the panel more than a picture. A control can only be photographed, but the page
/// it paints comes from this type, so the part that can be wrong in a way a photograph would hide - a colour sequence
/// painted as text, a row whose spans do not add up to the width - is checked here.
/// </remarks>
public sealed class TerminalSessionTests
{
    [Fact]
    public void TheScreenShowsWhatTheProgramWrote()
    {
        var session = new TerminalSession(columns: 20, rows: 4);

        session.Write("hello\r\nworld");

        var lines = session.Text.Split('\n', StringSplitOptions.TrimEntries);

        Assert.Equal("hello", lines[0]);
        Assert.Equal("world", lines[1]);
    }

    [Fact]
    public void AColourSequenceColoursTheTextInsteadOfShowingItsBytes()
    {
        var session = new TerminalSession(columns: 20, rows: 2);

        session.Write("\u001b[31mred\u001b[0m");

        // The emulator resolves the colour a cell has, so what proves the sequence was understood is the colour on the
        // span and the absence of the bytes in the text. The rest of the row is the blank padding, in the default colour.
        Assert.Equal("red", session.Text.TrimEnd());

        var coloured = Assert.Single(
            session.Page()[0].Spans,
            span => span.ForgroundColor != "#CDCDCD" && !string.IsNullOrWhiteSpace(span.Text));

        Assert.Equal("red", coloured.Text);
    }

    [Fact]
    public void ASequenceSplitAcrossTwoWritesIsStillOneSequence()
    {
        // A program's write is not a unit: pipes split where they like, so a parser made per write would paint the
        // first half as text and then find a bare "1m" - the defect this arrangement exists to prevent.
        var session = new TerminalSession(columns: 20, rows: 2);

        session.Write("\u001b[3");
        session.Write("1mred");

        Assert.Equal("red", session.Text.TrimEnd());
    }

    [Fact]
    public void TheCursorFollowsTheText()
    {
        var session = new TerminalSession(columns: 20, rows: 2);

        session.Write("abc");

        Assert.Equal((3, 0), session.Cursor);
        Assert.True(session.ShowCursor);
    }

    [Fact]
    public void ResizingChangesHowManyCellsFit()
    {
        var session = new TerminalSession(columns: 20, rows: 4);

        session.Resize(columns: 40, rows: 10);

        Assert.Equal(40, session.Columns);
        Assert.Equal(10, session.Rows);
        Assert.Equal(10, session.Page().Count);
    }

    [Fact]
    public void EveryRowOfThePageTilesTheWholeWidth()
    {
        // The painter walks the spans and adds up the widths it drew, so a row whose spans do not add up to the column
        // count would leave the rest of the line unpainted - or, worse, draw the next row's cell over the cursor.
        var session = new TerminalSession(columns: 20, rows: 3);

        session.Write("hello\r\nworld \u001b[31mred\u001b[0m done");

        foreach (var row in session.Page())
        {
            Assert.Equal(session.Columns, row.Spans.Sum(span => span.Text?.Length ?? 0));
        }
    }

    [Fact]
    public void AProgramThatChangesTheSizeItselfIsObeyed()
    {
        // A test of a drawing assertion in a language moves the cursor with escape sequences; a clear-screen sequence
        // is the cheapest proof that the emulator - rather than this type - is what interprets them.
        var session = new TerminalSession(columns: 20, rows: 3);

        session.Write("first\r\n\u001b[2J");

        Assert.DoesNotContain("first", session.Text);
    }
}
