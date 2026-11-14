using Xunit;

namespace Projectional.Core.Tests;

public sealed class ModelIdTests
{
    [Fact]
    public void NewIdsAreUniqueAndNotEmpty()
    {
        var first = ModelId.New();
        var second = ModelId.New();

        Assert.False(first.IsEmpty);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TheDefaultValueIsEmpty()
    {
        Assert.True(default(ModelId).IsEmpty);
        Assert.Equal(ModelId.Empty, default(ModelId));
    }

    [Fact]
    public void AnEmptyGuidIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ModelId.From(Guid.Empty));
    }

    [Fact]
    public void AnIdRoundTripsThroughItsCanonicalText()
    {
        var id = ModelId.New();

        var parsed = ModelId.Parse(id.ToString());

        Assert.Equal(id, parsed);
        Assert.Equal(id.ToString(), parsed.ToString());
        Assert.Equal(36, id.ToString().Length);
    }

    [Fact]
    public void OnlyTheCanonicalLayoutIsAccepted()
    {
        var canonical = ModelId.New().ToString();

        Assert.Throws<FormatException>(() => ModelId.Parse("{" + canonical + "}"));
        Assert.Throws<FormatException>(() => ModelId.Parse(canonical.Replace("-", string.Empty, StringComparison.Ordinal)));
        Assert.Equal(ModelId.Parse(canonical), ModelId.Parse(canonical.ToUpperInvariant()));
    }

    [Fact]
    public void TextThatIsNotAGuidIsRejected()
    {
        Assert.Throws<FormatException>(() => ModelId.Parse("not-a-model-id"));
        Assert.False(ModelId.TryParse("not-a-model-id", out _));
    }

    [Fact]
    public void NoTextAtAllIsNotAnId()
    {
        Assert.False(ModelId.TryParse(null, out _));
        Assert.False(ModelId.TryParse(string.Empty, out _));
        Assert.False(ModelId.TryParse("00000000-0000-0000-0000-000000000000", out _));
    }

    [Fact]
    public void IdsAreOrderedByTheirValue()
    {
        var first = ModelId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var second = ModelId.From(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        Assert.True(first < second);
        Assert.True(second > first);
        Assert.True(first <= ModelId.From(first.Value));
        Assert.True(first >= ModelId.From(first.Value));
        Assert.True(first.CompareTo(second) < 0);
    }
}
