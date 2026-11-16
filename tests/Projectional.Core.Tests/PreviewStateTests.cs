using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The hover ladder: what a host shows while the pointer rests on an entry, and - the part that is worth testing -
/// what happens when it does not rest.
/// </summary>
/// <remarks>
/// The clock is a parameter, so nothing here waits. Every case is driven by <see cref="PreviewState.Tick"/>, which
/// is what the control's timer does, and the assertions are about the stage a host would draw.
/// </remarks>
public sealed class PreviewStateTests
{
    private static readonly TimeSpan Hover = TimeSpan.FromMilliseconds(10);

    [Fact]
    public void ARestingPointerClimbsTheLadderInOrder()
    {
        var state = new PreviewState();

        state.Hover("wrap");

        Assert.Equal(PreviewStage.Nothing, state.Stage);
        Assert.False(state.IsPreviewing);

        state.Tick(TimeSpan.FromMilliseconds(290));

        Assert.Equal(PreviewStage.Nothing, state.Stage);

        state.Tick(Hover);

        Assert.Equal(PreviewStage.Tooltip, state.Stage);
        Assert.False(state.IsPreviewing);

        state.Tick(TimeSpan.FromMilliseconds(400));

        Assert.Equal(PreviewStage.Preview, state.Stage);
        Assert.True(state.IsPreviewing);
        Assert.Equal("wrap", state.Entry);
    }

    [Fact]
    public void ALateTickReachesTheRungItPassed()
    {
        // The stage is decided by how long the entry has been hovered, not by how often the host ticked: a timer
        // that was busy for a second must not leave the tooltip showing for another second.
        var state = new PreviewState();

        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(PreviewStage.Preview, state.Stage);
    }

    [Fact]
    public void MovingToAnotherEntryStartsAgain()
    {
        // A preview is an answer about one edit, so it is never left standing while the next entry is considered.
        var state = new PreviewState();

        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(PreviewStage.Preview, state.Stage);

        state.Hover("negate");

        Assert.Equal(PreviewStage.Nothing, state.Stage);
        Assert.Equal("negate", state.Entry);

        state.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(PreviewStage.Preview, state.Stage);
    }

    [Fact]
    public void HoveringTheSameEntryAgainContinuesTheClock()
    {
        // This is what a pointer does when it crosses the gap between the bulb and the list: it leaves, arrives
        // back on the entry it was on, and the preview must not start over - otherwise a user resting on an entry
        // beside a gap would never see a preview at all.
        var state = new PreviewState();

        state.Hover("wrap");
        state.Tick(TimeSpan.FromMilliseconds(400));

        Assert.Equal(PreviewStage.Tooltip, state.Stage);

        state.LeaveList();
        state.Hover("wrap");

        Assert.Equal(PreviewStage.Tooltip, state.Stage);

        state.Tick(TimeSpan.FromMilliseconds(300));

        Assert.Equal(PreviewStage.Preview, state.Stage);
    }

    [Fact]
    public void LeavingTheEntryEndsEverythingImmediately()
    {
        var state = new PreviewState();

        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));
        state.Leave();

        Assert.Equal(PreviewStage.Nothing, state.Stage);
        Assert.Null(state.Entry);
    }

    [Fact]
    public void LeavingTheListKeepsThePreviewForTheGracePeriod()
    {
        var state = new PreviewState();

        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));
        state.LeaveList();

        // Still showing while the pointer is on its way.
        Assert.Equal(PreviewStage.Preview, state.Stage);
        Assert.Equal("wrap", state.Entry);

        state.Tick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(PreviewStage.Preview, state.Stage);

        state.Tick(TimeSpan.FromMilliseconds(60));

        Assert.Equal(PreviewStage.Nothing, state.Stage);
        Assert.Null(state.Entry);
    }

    [Fact]
    public void TheGracePeriodDoesNotClimbTheLadderWhileThePointerIsAway()
    {
        // The clock is paused, not merely tolerant: a pointer that leaves on the tooltip rung and comes back must
        // find the tooltip, not a preview it never waited for.
        var state = new PreviewState();

        state.Hover("wrap");
        state.Tick(TimeSpan.FromMilliseconds(400));
        state.LeaveList();
        state.Tick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(PreviewStage.Tooltip, state.Stage);

        state.Hover("wrap");
        state.Tick(TimeSpan.FromMilliseconds(100));

        Assert.Equal(PreviewStage.Tooltip, state.Stage);
    }

    [Fact]
    public void AChangedDocumentSilencesTheHoverUntilItIsAskedAgain()
    {
        // "A model change during the delay is cancelled, and not restarted by the same hover." Cancelling without
        // remembering would restart the ladder on the next tick, and a preview would flicker while the user edits.
        var state = new PreviewState();

        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));
        state.Invalidate();

        Assert.Equal(PreviewStage.Nothing, state.Stage);
        Assert.Equal("wrap", state.Entry);

        state.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(PreviewStage.Nothing, state.Stage);

        // Asking again is a pointer that leaves and returns, and that is allowed.
        state.Leave();
        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(PreviewStage.Preview, state.Stage);
    }

    [Fact]
    public void AnInvalidatedEntryDoesNotSilenceTheNextOne()
    {
        // The memory is about the entry that was stale, not about the list.
        var state = new PreviewState();

        state.Hover("wrap");
        state.Invalidate();
        state.Hover("negate");
        state.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(PreviewStage.Preview, state.Stage);
        Assert.Equal("negate", state.Entry);
    }

    [Fact]
    public void AListThatWasNeverEnteredHasNothingToKeep()
    {
        var state = new PreviewState();

        state.LeaveList();

        Assert.Equal(PreviewStage.Nothing, state.Stage);
        Assert.Null(state.Entry);
    }

    [Fact]
    public void AHostWithoutAGracePeriodLosesThePreviewAtOnce()
    {
        // Zero is a legal grace period and means "cancel as soon as the pointer leaves", which is the honest
        // reading of a host that asked for no grace.
        var state = new PreviewState(PreviewDelay.Default with { Grace = TimeSpan.Zero });

        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));
        state.LeaveList();

        Assert.Equal(PreviewStage.Nothing, state.Stage);
    }

    [Fact]
    public void TheClockDoesNotRunBackwards()
    {
        var state = new PreviewState();

        state.Hover("wrap");

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void AnEntryWithoutAnIdIsRefused()
    {
        var state = new PreviewState();

        Assert.Throws<ArgumentException>(() => state.Hover(""));
        Assert.Throws<ArgumentException>(() => state.Hover(" "));
    }

    [Fact]
    public void ADelayWithItsRungsOutOfOrderIsRefusedWhenTheStateIsBuilt()
    {
        // The value object can be constructed invalid so that a preferences dialog can ask IsValid; the machine
        // that would act on it refuses to exist.
        var broken = new PreviewDelay(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => new PreviewState(broken));
    }

    [Fact]
    public void TheLadderPrintsWhatItIsShowing()
    {
        var state = new PreviewState();

        Assert.Equal("none (Nothing)", state.ToString());

        state.Hover("wrap");
        state.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal("wrap (Preview)", state.ToString());
    }
}
