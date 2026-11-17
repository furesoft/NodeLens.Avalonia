using Projectional.Core;
using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The widths a reader dragged a table's columns to: a value, keyed by the table and the column, that
/// never touches the model.
/// </summary>
public sealed class ColumnWidthsTests
{
    private static readonly NodeId Owner = NodeId.New();

    [Fact]
    public void AnEmptySetAnswersNothingForEveryColumn()
    {
        Assert.True(ColumnWidths.Empty.IsEmpty);
        Assert.Null(ColumnWidths.Empty.WidthOf(Owner, "steps", 0));
    }

    [Fact]
    public void AWidthIsAnsweredForTheColumnItWasSetOn()
    {
        var widths = ColumnWidths.Empty.With(Owner, "steps", 1, 12);

        Assert.False(widths.IsEmpty);
        Assert.Equal(12, widths.WidthOf(Owner, "steps", 1));
        Assert.Null(widths.WidthOf(Owner, "steps", 0));
        Assert.Null(widths.WidthOf(Owner, "other", 1));
        Assert.Null(widths.WidthOf(NodeId.New(), "steps", 1));
    }

    [Fact]
    public void SettingAWidthAnswersANewValueAndLeavesTheOldOneAlone()
    {
        var first = ColumnWidths.Empty.With(Owner, "steps", 0, 8);
        var second = first.With(Owner, "steps", 0, 20);

        Assert.Equal(8, first.WidthOf(Owner, "steps", 0));
        Assert.Equal(20, second.WidthOf(Owner, "steps", 0));
    }

    [Fact]
    public void AColumnGoesBackToItsContentWidthWhenItsWidthIsRemoved()
    {
        var widths = ColumnWidths.Empty.With(Owner, "steps", 0, 8).Without(Owner, "steps", 0);

        Assert.True(widths.IsEmpty);
        Assert.Null(widths.WidthOf(Owner, "steps", 0));
    }

    [Fact]
    public void RemovingAWidthThatWasNeverSetChangesNothing()
    {
        var widths = ColumnWidths.Empty.With(Owner, "steps", 0, 8);

        Assert.Same(widths, widths.Without(Owner, "steps", 3));
    }

    [Fact]
    public void EveryWidthOfOneNodeCanBeForgottenAtOnce()
    {
        var other = NodeId.New();
        var widths = ColumnWidths.Empty
            .With(Owner, "steps", 0, 8)
            .With(Owner, "steps", 1, 9)
            .With(other, "steps", 0, 10)
            .Without(Owner);

        Assert.Null(widths.WidthOf(Owner, "steps", 0));
        Assert.Null(widths.WidthOf(Owner, "steps", 1));
        Assert.Equal(10, widths.WidthOf(other, "steps", 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AWidthThatIsNotAPositiveFiniteNumberIsRefused(double width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnWidths.Empty.With(Owner, "steps", 0, width));
    }

    [Fact]
    public void AColumnIndexBelowZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnWidths.Empty.With(Owner, "steps", -1, 8));
    }
}
