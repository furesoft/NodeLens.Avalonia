using Projectional.Checks;
using Projectional.Core;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The ordered, immutable list of checks one language offers.
/// </summary>
/// <remarks>
/// The set is the composition point a language author writes once, so the two things worth pinning are that
/// it keeps the order it was given and that adding a check cannot change the set a document already holds.
/// </remarks>
public sealed class CheckSetTests
{
    [Fact]
    public void AnEmptySetFindsNothing()
    {
        Assert.Empty(CheckSet.Empty.Checks);
        Assert.Empty(CheckSet.Empty.Find(TestLanguage.NewModel()));
    }

    [Fact]
    public void EveryCheckRunsInTheOrderTheSetWasBuiltWith()
    {
        var first = new StubCheck("first");
        var second = new StubCheck("second");
        var set = new CheckSet([first, second]);

        var fixes = set.Find(PayrollSample.NewEmptyModel());

        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
        Assert.Equal(["first", "second"], fixes.Select(fix => fix.Label));
    }

    [Fact]
    public void AddingACheckAnswersANewSet()
    {
        var set = new CheckSet([new StubCheck("first")]);
        var extended = set.With(new StubCheck("second"));

        Assert.Equal(1, set.Count);
        Assert.Equal(2, extended.Count);
        Assert.IsType<StubCheck>(Assert.Single(set.Checks));
        Assert.IsType<StubCheck>(extended.Checks[1]);
    }

    [Fact]
    public void ACheckThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CheckSet(null!));
        Assert.Throws<ArgumentNullException>(() => new CheckSet([new StubCheck("first"), null!]));
        Assert.Throws<ArgumentNullException>(() => CheckSet.Empty.With(null!));
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CheckSet.Empty.Find(null!));
    }
}
