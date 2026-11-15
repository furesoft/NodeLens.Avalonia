using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Caret movement is structural where the notation has an order and geometric where it does not, so
/// the tests check both: stepping from cell to cell along a line, and landing on the line above or
/// below.
/// </summary>
/// <remarks>
/// A position names a cell by address, and a node's keywords, spaces and punctuation all carry that
/// node's address, so an expectation about "the cell the caret is in" cannot be expressed by the
/// address alone. Every assertion therefore reads the cell through
/// <see cref="CaretNavigation.PlaceAt(EditorTree, CaretPosition)"/> and checks the text of it. The
/// bare rule lays out as <c>rule unnamed as &lt;none&gt; {</c> over an empty body slot and an empty
/// extends slot, one cell per notation cell, so the expected offsets can be counted off the notation.
/// </remarks>
public sealed class CaretNavigationTests
{
    [Fact]
    public void FirstLandsOnTheFirstKeyword()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var first = CaretNavigation.First(session.Tree);

        Assert.Equal("rule", Place(session, first).Text);
        Assert.Equal(0, first.Offset);
    }

    [Fact]
    public void LastLandsAfterTheLastCell()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var last = CaretNavigation.Last(session.Tree);

        Assert.Equal("}", Place(session, last).Text);
        Assert.Equal(1, last.Offset);
    }

    [Fact]
    public void NearestKeepsTheCellAndClampsTheOffsetToItsText()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var clamped = CaretNavigation.Nearest(session.Tree, CaretPosition.At(name, 99));

        Assert.Equal(name, clamped.Address);
        Assert.Equal("unnamed".Length, clamped.Offset);
    }

    [Fact]
    public void NearestEntersTheSubtreeAContainerAddressBelongsTo()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        // The address of the body element is the container the body's cells sit in, not a place the
        // caret can stand in, so it resolves to the first cell inside it.
        var nearest = CaretNavigation.Nearest(session.Tree, CaretPosition.At(CellAddress.OfElement(rule.Id, "body", 0)));

        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), nearest.Address);
        Assert.Equal(0, nearest.Offset);
    }

    [Fact]
    public void NearestFromAnAddressOfAnotherModelLandsOnTheFirstCell()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());
        var foreign = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var nearest = CaretNavigation.Nearest(session.Tree, CaretPosition.At(new CellAddress(foreign.Id)));

        Assert.Equal("rule", Place(session, nearest).Text);
        Assert.Equal(0, nearest.Offset);
    }

    [Fact]
    public void RightMovesToTheNextCellEvenFromTheMiddleOfOne()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var moved = Move(session, CaretMove.Right);

        Assert.Equal(" ", Place(session, moved).Text);
        Assert.Equal(0, moved.Offset);
    }

    [Fact]
    public void LeftFromTheMiddleOfACellMovesAWholeCell()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var moved = CaretNavigation.Move(
            session.Layout,
            session.Measure,
            CaretPosition.At(name, 3),
            CaretMove.Left,
            page: 0);

        Assert.Equal(" ", Place(session, moved).Text);
        Assert.Equal(1, moved.Offset);
    }

    [Fact]
    public void RightCharacterStepsInsideTheCell()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var moved = Move(session, CaretMove.RightCharacter);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), moved.Address);
        Assert.Equal(1, moved.Offset);
    }

    [Fact]
    public void LeftCharacterCrossesIntoThePreviousCellAtItsStart()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var moved = Move(session, CaretMove.LeftCharacter);

        Assert.Equal(" ", Place(session, moved).Text);
        Assert.Equal(1, moved.Offset);
    }

    [Fact]
    public void ACharacterMoveStaysAtTheEndOfTheLastCell()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var moved = CaretNavigation.Move(
            session.Layout,
            session.Measure,
            CaretNavigation.Last(session.Tree),
            CaretMove.RightCharacter,
            page: 0);

        Assert.Equal("}", Place(session, moved).Text);
        Assert.Equal(1, moved.Offset);
    }

    [Fact]
    public void RightFromTheEndOfACellLandsAtTheStartOfTheNext()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var name = CellAddress.OfSlot(rule.Id, "name");

        var moved = CaretNavigation.Move(
            session.Layout,
            session.Measure,
            CaretPosition.At(name, "unnamed".Length),
            CaretMove.Right,
            page: 0);

        Assert.Equal(" ", Place(session, moved).Text);
        Assert.Equal(0, moved.Offset);
    }

    [Fact]
    public void LeftFromTheStartOfACellLandsAtTheEndOfThePrevious()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var moved = Move(session, CaretMove.Left);

        Assert.Equal(" ", Place(session, moved).Text);
        Assert.Equal(1, moved.Offset);
    }

    [Fact]
    public void LeftAtTheFirstCellStays()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var moved = CaretNavigation.Move(
            session.Layout,
            session.Measure,
            CaretNavigation.First(session.Tree),
            CaretMove.Left,
            page: 0);

        Assert.Equal("rule", Place(session, moved).Text);
        Assert.Equal(0, moved.Offset);
    }

    [Fact]
    public void RightAtTheLastCellStays()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var moved = CaretNavigation.Move(
            session.Layout,
            session.Measure,
            CaretNavigation.Last(session.Tree),
            CaretMove.Right,
            page: 0);

        Assert.Equal("}", Place(session, moved).Text);
        Assert.Equal(1, moved.Offset);
    }

    [Fact]
    public void EndLandsOnTheLastCellOfTheLineAndHomeReturnsToTheFirst()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var end = Move(session, CaretMove.LineEnd);

        Assert.Equal("{", Place(session, end).Text);
        Assert.Equal(1, end.Offset);

        var home = Move(session, CaretMove.LineStart, end);

        Assert.Equal("rule", Place(session, home).Text);
        Assert.Equal(0, home.Offset);
    }

    [Fact]
    public void UpFromTheFirstLineStaysWhereItIs()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var moved = Move(session, CaretMove.Up);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), moved.Address);
        Assert.Equal(0, moved.Offset);
    }

    [Fact]
    public void DownReachesTheIndentedBodySlot()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);

        var moved = Move(session, CaretMove.Down);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "body"), moved.Address);
        Assert.True(Place(session, moved).IsEmptySlot);
    }

    [Fact]
    public void DownWalksTheLinesOfTheBodyBeforeLeavingIt()
    {
        var (rule, body) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        // An empty body still occupies a line per slot of its own notation, so the caret walks those
        // before it reaches the extends slot of the rule.
        var steps = Move(session, CaretMove.Down);
        var notes = Move(session, CaretMove.Down, steps);
        var extends = Move(session, CaretMove.Down, notes);

        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), steps.Address);
        Assert.Equal(CellAddress.OfSlot(body.Id, "notes"), notes.Address);
        Assert.Equal("extends", Place(session, extends).Text);
        Assert.Equal(0, extends.Offset);
    }

    [Fact]
    public void UpFromTheBodySlotKeepsTheColumnItWasIn()
    {
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule);

        // The body is indented, so its column falls inside the keyword of the line above rather than
        // at the start of the name.
        var above = Move(session, CaretMove.Up, Move(session, CaretMove.Down));

        Assert.Equal("rule", Place(session, above).Text);
        Assert.Equal(2, above.Offset);
    }

    [Fact]
    public void UpFromADeepLineLandsOnTheCellAboveTheSameColumn()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        // The text of the step starts at column six, which is the second character of the name.
        session.Select(Selection.At(CellAddress.OfSlot(step.Id, "text")));
        var above = Move(session, CaretMove.Up);

        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), above.Address);
        Assert.Equal(1, above.Offset);
    }

    [Fact]
    public void DownFromTheKeywordLineEntersTheChildElement()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);

        var below = Move(session, CaretMove.Down);

        Assert.Equal(step.Id, below.Address.Owner);
    }

    [Fact]
    public void APageMoveWithoutAViewportIsASingleLineMove()
    {
        var (rule, _, _) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(rule);
        var start = CaretNavigation.First(session.Tree);

        // Zero is what a host that never reported a viewport height sends, so the page keys have to
        // degrade to the arrow instead of jumping somewhere the user cannot see.
        var down = Move(session, CaretMove.Down, start);
        var paged = Move(session, CaretMove.PageDown, start, page: 0);

        Assert.Equal(down.Address, paged.Address);
        Assert.Equal(down.Offset, paged.Offset);
    }

    [Fact]
    public void PageDownMovesAsManyLinesAsTheViewportHolds()
    {
        var (body, _) = TestEditor.BodyWithSteps("a", "b", "c", "d");
        var rule = body.Parent ?? throw new InvalidOperationException("The body hangs under no rule.");
        using var session = TestEditor.Open(rule);
        var start = CaretNavigation.First(session.Tree);

        // One line of text is one layout unit, so a five-unit page moves four lines: the last line of
        // the page has to stay below the first one for the page to have covered anything.
        var paged = Move(session, CaretMove.PageDown, start, page: 5 * session.Measure.LineHeight);

        Assert.Equal(
            LineOf(session, start) + (4 * session.Measure.LineHeight),
            LineOf(session, paged));

        // A page is not a jump to the end: the line above the target still has somewhere to hold the
        // caret, and the single-line move must stay where it was.
        Assert.True(LineOf(session, paged) > LineOf(session, Move(session, CaretMove.Down, start)));
    }

    [Fact]
    public void PageUpAndPageDownClampToTheDocument()
    {
        var (body, _) = TestEditor.BodyWithSteps("a", "b", "c", "d");
        var rule = body.Parent ?? throw new InvalidOperationException("The body hangs under no rule.");
        using var session = TestEditor.Open(rule);
        var start = CaretNavigation.First(session.Tree);

        // A page taller than the document asks for a line that does not exist, so it lands on the last
        // one instead of refusing to move: a page key that does nothing looks broken.
        var bottom = Move(session, CaretMove.PageDown, start, page: 1000);
        var top = Move(session, CaretMove.PageUp, bottom, page: 1000);

        Assert.Equal(session.Layout.Size.Height - session.Measure.LineHeight, LineOf(session, bottom));
        Assert.Equal(0, LineOf(session, top));
    }

    [Fact]
    public void DocumentStartAndEndAreReachableThroughTheMoves()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        var end = Move(session, CaretMove.DocumentEnd);
        var start = Move(session, CaretMove.DocumentStart, end);

        Assert.Equal("}", Place(session, end).Text);
        Assert.Equal(1, end.Offset);
        Assert.Equal("rule", Place(session, start).Text);
        Assert.Equal(0, start.Offset);
    }

    [Fact]
    public void AnUnknownMoveIsRefused()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CaretNavigation.Move(session.Layout, session.Measure, session.Selection.Caret, (CaretMove)99, 0));
    }

    [Fact]
    public void AMoveFromASlotTheConceptDoesNotHaveStartsAtTheFirstCell()
    {
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule);
        var nowhere = CaretPosition.At(CellAddress.OfSlot(rule.Id, "steps"));

        var moved = CaretNavigation.Move(session.Layout, session.Measure, nowhere, CaretMove.DocumentStart, 0);

        Assert.Equal("rule", Place(session, moved).Text);
        Assert.Equal(0, moved.Offset);
    }

    private static CaretPosition Move(
        EditorSession session,
        CaretMove move,
        CaretPosition? from = null,
        double page = 0) =>
        CaretNavigation.Move(session.Layout, session.Measure, from ?? session.Selection.Caret, move, page);

    private static CaretPlace Place(EditorSession session, CaretPosition position) =>
        CaretNavigation.PlaceAt(session.Tree, position)
        ?? throw new InvalidOperationException($"Nothing stands at {position}.");

    private static double LineOf(EditorSession session, CaretPosition position) =>
        CaretBounds.Of(session.Tree, session.Layout, position, session.Measure)?.Y
        ?? throw new InvalidOperationException($"No caret bar stands at {position}.");
}
