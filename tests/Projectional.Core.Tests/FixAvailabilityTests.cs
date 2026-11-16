using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Availability is the two-state answer a fix gives: it can run, or it is listed and cannot.
/// </summary>
/// <remarks>
/// The distinction that matters is that a disabled fix still exists. Dropping it would turn "this
/// language knows about the problem and here is why it cannot act" into silence, which is the half of
/// the message a user can act on.
/// </remarks>
public sealed class FixAvailabilityTests
{
    [Fact]
    public void AppliesCarriesNoReason()
    {
        Assert.False(FixAvailability.Applies.IsDisabled);
        Assert.Null(FixAvailability.Applies.Reason);
    }

    [Fact]
    public void TheDefaultValueApplies()
    {
        // A fix's availability is optional, so the value a caller gets by omitting it has to be the
        // permissive answer rather than a disabled one with no reason to show.
        Assert.False(default(FixAvailability).IsDisabled);
    }

    [Fact]
    public void DisabledCarriesItsReason()
    {
        var availability = FixAvailability.Disabled("the document is read-only");

        Assert.True(availability.IsDisabled);
        Assert.Equal("the document is read-only", availability.Reason);
    }

    [Fact]
    public void DisabledNeedsAReason()
    {
        Assert.Throws<ArgumentException>(() => FixAvailability.Disabled(string.Empty));
        Assert.Throws<ArgumentNullException>(() => FixAvailability.Disabled(null!));
    }

    [Fact]
    public void TwoDisabledReasonsAreNotTheSameAvailability()
    {
        Assert.NotEqual(FixAvailability.Disabled("one"), FixAvailability.Disabled("two"));
        Assert.Equal(FixAvailability.Disabled("one"), FixAvailability.Disabled("one"));
    }
}
