using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A status bar says where the caret is, and a projectional document has no lines of its own: the line is
/// the numbered row the caret is drawn on, and the column is its characters into the cell that draws it.
/// </summary>
/// <remarks>
/// The fixtures come from <see cref="TestEditor"/> and are measured in monospace columns, so one line is one
/// unit tall and a row can be expected rather than rendered.
/// </remarks>
public sealed class CaretSpotTests
{
    [Fact]
    public void ANestedNodeStartsOnTheRowBelowItsOwner()
    {
        var (rule, _, step) = TestEditor.RuleWithStep();

        using var session = TestEditor.Open(rule);

        session.Select(Selection.At(new CaretPosition(CellAddress.OfSlot(step.Id, "text"))));

        Assert.Equal(new CaretSpot(2, 1), SpotOf(session));
    }

    [Fact]
    public void TheColumnCountsCharactersIntoTheCaretSCell()
    {
        var step = TestEditor.BareStep("walk");

        using var session = TestEditor.Open(step);

        session.Select(Selection.At(new CaretPosition(CellAddress.OfSlot(step.Id, "text"), 2)));

        Assert.Equal(new CaretSpot(1, 3), SpotOf(session));
    }

    [Fact]
    public void TheEndOfAValueIsOneColumnPastItsLastCharacter()
    {
        var step = TestEditor.BareStep("walk");

        using var session = TestEditor.Open(step);

        session.Select(Selection.At(new CaretPosition(CellAddress.OfSlot(step.Id, "text"), 4)));

        Assert.Equal(new CaretSpot(1, 5), SpotOf(session));
    }

    [Fact]
    public void ACaretOnASecondDrawnLineIsOnASecondRowAndStartsItsColumnAgain()
    {
        // A hard break in the value is drawn on two lines, and the second one starts at the cell's left edge
        // again - so it is a second row and the caret at its start is in the first column.
        var step = TestEditor.BareStep("a\nb");

        using var session = TestEditor.Open(step);

        session.Select(Selection.At(new CaretPosition(CellAddress.OfSlot(step.Id, "text"), 2)));

        Assert.Equal(new CaretSpot(2, 1), SpotOf(session));
    }

    private static CaretSpot SpotOf(EditorSession session) =>
        CaretSpot.Of(session) ?? throw new InvalidOperationException("The caret of this document draws nowhere.");
}
