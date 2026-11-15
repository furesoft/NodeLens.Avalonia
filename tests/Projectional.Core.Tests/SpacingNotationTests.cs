using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Spacing is declared on the notation, because how wide a gap is belongs to the language and not to
/// the projection that happens to draw it.
/// </summary>
public sealed class SpacingNotationTests
{
    [Fact]
    public void ACollectionIsSpacedByOneUnlessItSaysOtherwise()
    {
        var notation = new NotationEditor().Collection("items");

        Assert.Equal(1, notation.Spacing);
    }

    [Fact]
    public void ACollectionKeepsTheSpacingItDeclares()
    {
        var notation = new NotationEditor().Collection("items", separator: null, spacing: 0);

        Assert.Equal(0, notation.Spacing);
    }

    [Fact]
    public void AChildSlotKeepsTheSpacingItDeclares()
    {
        var notation = new NotationEditor().Children("items", spacing: 3);

        Assert.Equal(3, notation.Spacing);
    }

    [Fact]
    public void ACollectionCannotBeSpacedNegatively() => Assert.Throws<ArgumentOutOfRangeException>(
        () => new NotationEditor().Collection("items", separator: null, spacing: -1));

    [Fact]
    public void AChildSlotCannotBeSpacedNegatively() => Assert.Throws<ArgumentOutOfRangeException>(
        () => new NotationEditor().Children("items", spacing: -1));
}
