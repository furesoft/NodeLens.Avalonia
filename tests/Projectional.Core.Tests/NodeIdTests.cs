using Xunit;

namespace Projectional.Core.Tests;

public sealed class NodeIdTests
{
    [Fact]
    public void NewIdsAreUniqueAndNotEmpty()
    {
        var first = NodeId.New();
        var second = NodeId.New();

        Assert.False(first.IsEmpty);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DefaultValueIsTheOnlyEmptyId()
    {
        Assert.True(default(NodeId).IsEmpty);
        Assert.False(NodeId.New().IsEmpty);
    }

    [Fact]
    public void ToStringUsesTheCanonicalUuidForm()
    {
        var text = NodeId.New().ToString();

        Assert.Equal(36, text.Length);
        Assert.Equal(text.ToLowerInvariant(), text);
        Assert.Equal(4, text.Count(c => c == '-'));
    }

    [Fact]
    public void ParsingItsOwnTextRoundTrips()
    {
        var id = NodeId.New();

        Assert.Equal(id, NodeId.Parse(id.ToString()));
        Assert.True(NodeId.TryParse(id.ToString(), out var parsed));
        Assert.Equal(id, parsed);
    }

    [Theory]
    [InlineData("{6F9619FF-8B86-D011-B42D-00C04FC964FF}")]
    [InlineData("6f9619ff8b86d011b42d00c04fc964ff")]
    [InlineData("(6f9619ff-8b86-d011-b42d-00c04fc964ff)")]
    public void NonCanonicalLayoutsAreRejectedSoOneIdCannotHaveTwoForms(string text)
    {
        Assert.False(NodeId.TryParse(text, out _));
        Assert.Throws<FormatException>(() => NodeId.Parse(text));
    }

    [Fact]
    public void HexLetterCaseDoesNotCreateADifferentId()
    {
        const string Lower = "6f9619ff-8b86-d011-b42d-00c04fc964ff";
        const string Upper = "6F9619FF-8B86-D011-B42D-00C04FC964FF";

        Assert.True(NodeId.TryParse(Upper, out var upper));
        Assert.Equal(NodeId.Parse(Lower), upper);
        Assert.Equal(Lower, upper.ToString());
    }

    [Fact]
    public void TheEmptyGuidTextIsNotAValidId()
    {
        Assert.False(NodeId.TryParse("00000000-0000-0000-0000-000000000000", out _));
        Assert.Throws<FormatException>(() => NodeId.Parse("00000000-0000-0000-0000-000000000000"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-id")]
    public void GarbageIsRejected(string? text)
    {
        Assert.False(NodeId.TryParse(text, out _));
    }

    [Fact]
    public void EmptyGuidIsNotAValidId()
    {
        Assert.Throws<ArgumentException>(() => NodeId.From(Guid.Empty));
    }

    [Fact]
    public void OrderingIsStableAndConsistent()
    {
        var low = NodeId.From(new Guid("00000000-0000-0000-0000-000000000001"));
        var high = NodeId.From(new Guid("00000000-0000-0000-0000-000000000002"));

        Assert.True(low < high);
        Assert.True(low <= high);
        Assert.True(high > low);
        Assert.True(high >= low);
        Assert.True(low.CompareTo(high) < 0);
        Assert.Equal(0, low.CompareTo(NodeId.From(low.Value)));
    }
}
