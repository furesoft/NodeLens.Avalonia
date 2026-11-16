using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The three answers an intention gives about a caret: applies, disabled with a reason, or absent.
/// </summary>
/// <remarks>
/// Absent and disabled are the pair worth pinning, because they look alike and mean different things: one is
/// dropped from the list, the other is shown greyed with its reason.
/// </remarks>
public sealed class IntentionAvailabilityTests
{
    [Fact]
    public void TheDefaultValueApplies()
    {
        // An intention that says nothing is offered rather than silently hidden, so omitting the argument
        // cannot turn a declaration into a permanent no.
        Assert.True(default(IntentionAvailability).IsApplicable);
        Assert.False(default(IntentionAvailability).IsAbsent);
        Assert.False(default(IntentionAvailability).IsDisabled);
    }

    [Fact]
    public void AbsentIsNeitherApplicableNorDisabled()
    {
        var absent = IntentionAvailability.Absent;

        Assert.True(absent.IsAbsent);
        Assert.False(absent.IsApplicable);
        Assert.False(absent.IsDisabled);
        Assert.Null(absent.Reason);
    }

    [Fact]
    public void DisabledKeepsItsReason()
    {
        var availability = IntentionAvailability.Disabled("the document is read-only");

        Assert.True(availability.IsDisabled);
        Assert.False(availability.IsAbsent);
        Assert.False(availability.IsApplicable);
        Assert.Equal("the document is read-only", availability.Reason);
    }

    [Fact]
    public void DisabledNeedsAReason()
    {
        Assert.Throws<ArgumentException>(() => IntentionAvailability.Disabled(string.Empty));
        Assert.Throws<ArgumentNullException>(() => IntentionAvailability.Disabled(null!));
    }

    [Fact]
    public void TheThreeStatesAreDistinctValues()
    {
        Assert.NotEqual(IntentionAvailability.Applies, IntentionAvailability.Absent);
        Assert.NotEqual(IntentionAvailability.Applies, IntentionAvailability.Disabled("why not"));
        Assert.NotEqual(IntentionAvailability.Absent, IntentionAvailability.Disabled("why not"));
        Assert.Equal(IntentionAvailability.Disabled("one"), IntentionAvailability.Disabled("one"));
    }

    [Fact]
    public void EachStateSaysItsName()
    {
        Assert.Equal("applies", IntentionAvailability.Applies.ToString());
        Assert.Equal("absent", IntentionAvailability.Absent.ToString());
        Assert.Equal("disabled: no", IntentionAvailability.Disabled("no").ToString());
    }
}
