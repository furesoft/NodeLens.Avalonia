using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The four numbers the hover ladder is measured with, which are tuning and are allowed to be wrong - so they are
/// checked before a session can use them rather than at the first hover.
/// </summary>
public sealed class PreviewDelayTests
{
    [Fact]
    public void TheDefaultIsAThirdOfASecondAndSevenTenths()
    {
        var delay = PreviewDelay.Default;

        Assert.Equal(TimeSpan.Zero, delay.Highlight);
        Assert.Equal(TimeSpan.FromMilliseconds(300), delay.Description);
        Assert.Equal(TimeSpan.FromMilliseconds(700), delay.Preview);
        Assert.Equal(TimeSpan.FromMilliseconds(150), delay.Grace);
        Assert.True(delay.IsValid);
    }

    [Fact]
    public void ANegativeDurationIsRefused()
    {
        var delay = new PreviewDelay(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(700),
            TimeSpan.FromMilliseconds(-1));

        Assert.False(delay.IsValid);
        Assert.Throws<ArgumentException>(delay.Validate);
    }

    [Fact]
    public void ADocumentThatIsShownBeforeItsDescriptionIsRefused()
    {
        // The rungs are a ladder, so the numbers have to climb: a preview that appears at the same time as the
        // tooltip would show the answer before the question.
        var delay = new PreviewDelay(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(700),
            TimeSpan.FromMilliseconds(700),
            TimeSpan.FromMilliseconds(150));

        Assert.False(delay.IsValid);
        Assert.Throws<ArgumentException>(delay.Validate);
    }

    [Fact]
    public void AHighlightLaterThanItsDescriptionIsRefused()
    {
        var delay = new PreviewDelay(
            TimeSpan.FromMilliseconds(400),
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(700),
            TimeSpan.FromMilliseconds(150));

        Assert.False(delay.IsValid);
        Assert.Throws<ArgumentException>(delay.Validate);
    }

    [Fact]
    public void AHostMayTurnTheLadderIntoASwitch()
    {
        // Zero for every rung is legal and means "show everything at once": a host that wants no ladder is not a
        // host with an invalid setting. Only the ordering rule survives, and zero satisfies it as long as the
        // preview is still the last rung.
        var instant = new PreviewDelay(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.Zero);

        Assert.True(instant.IsValid);
    }
}
