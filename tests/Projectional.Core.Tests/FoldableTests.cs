using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The permission is model data: a concept nominates a boolean slot, and each node's own value decides.
/// </summary>
public sealed class FoldableTests
{
    [Fact]
    public void ANodeWhoseSlotIsTrueIsFoldable()
    {
        // The fixture's collapse slot is `enabled`, which defaults to true - so a node of that concept folds
        // unless it opts out, without a line of code.
        Assert.True(Foldable.Of(TestEditor.BareRule()));
    }

    [Fact]
    public void ANodeWhoseSlotIsFalseIsNotFoldable()
    {
        var rule = TestEditor.BareRule();

        // `enabled` is not drawn by the fixture's notation, so it is written through the model rather than
        // through a cell: a SetPropertyAction addresses a cell and there is none.
        using (rule.Model.BeginMutation())
        {
            rule.Set("enabled", false);
        }

        Assert.False(Foldable.Of(rule));
    }

    [Fact]
    public void ANodeOfAConceptThatNominatesNoSlotIsNotFoldable()
    {
        Assert.False(Foldable.Of(TestEditor.BareStep()));
    }

    [Fact]
    public void TheNodeIsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => Foldable.Of(null!));
    }
}
