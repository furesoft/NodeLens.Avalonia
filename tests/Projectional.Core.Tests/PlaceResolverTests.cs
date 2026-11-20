using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>What a list row selects when it is chosen, which is a question about an address rather than a row.</summary>
/// <remarks>
/// A row is a snapshot and the model is not: the findings and usages are derived at one moment, the list outlives it,
/// and the user can delete the node a row names or switch to a variant that hides its cell. Selecting a place that
/// draws nothing is indistinguishable from the click being ignored, so each of the three cases is answered here once
/// for both panels.
/// </remarks>
public sealed class PlaceResolverTests
{
    [Fact]
    public void APlaceTheTreeDrawsIsThePlace()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var place = CellAddress.OfSlot(rule.Id, "name");

        Assert.NotNull(session.Tree.Find(place));
        Assert.Equal(place, PlaceResolver.Resolve(session, place));
    }

    [Fact]
    public void ANodeAddressIsAPlaceOfItsOwn()
    {
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var place = new CellAddress(rule.Id);

        Assert.Equal(place, PlaceResolver.Resolve(session, place));
    }

    [Fact]
    public void ASlotTheNotationHidesFallsBackToItsNode()
    {
        // The body's steps are drawn, its `note` is not - an address to a hidden slot would select nothing, so the
        // node is the nearest place a reader can be sent to.
        var rule = TestEditor.BareRule();

        using var session = TestEditor.Open(rule);

        var place = CellAddress.OfSlot(rule.Id, "note");

        Assert.Null(session.Tree.Find(place));
        Assert.Equal(new CellAddress(rule.Id), PlaceResolver.Resolve(session, place));
    }

    [Fact]
    public void ARowWhoseNodeIsGoneHasNowhereToGo()
    {
        // The document is edited after the findings were derived, which is the only way a row can name a node that no
        // longer exists - and the panel must not select it as though it did.
        var (rule, body) = TestEditor.RuleWithBody();

        using var session = TestEditor.Open(rule);

        Assert.Equal(new CellAddress(body.Id), PlaceResolver.Resolve(session, new CellAddress(body.Id)));

        session.Apply(new DeleteNodeAction(body.Id));

        Assert.Null(PlaceResolver.Resolve(session, new CellAddress(body.Id)));
    }

    [Fact]
    public void ASessionIsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => PlaceResolver.Resolve(null!, new CellAddress(NodeId.New())));
    }
}
