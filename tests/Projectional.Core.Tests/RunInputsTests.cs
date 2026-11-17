using Projectional.Language;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The one line the output panel offers, read as the variables a run is given.</summary>
public sealed class RunInputsTests
{
    [Fact]
    public void AnEmptyLineNamesNothing()
    {
        Assert.True(RunInputs.TryParse(null, out var none, out var noError));
        Assert.Empty(none);
        Assert.Null(noError);

        Assert.True(RunInputs.TryParse("   ", out var blank, out _));
        Assert.Empty(blank);
    }

    [Fact]
    public void ANumberIsANumber()
    {
        Assert.True(RunInputs.TryParse("amount=1500", out var variables, out _));

        Assert.Equal(new IntValue(1500), Assert.Contains("amount", variables));
    }

    [Fact]
    public void AFlagIsAFlag()
    {
        Assert.True(RunInputs.TryParse("approved=true", out var variables, out _));

        Assert.Equal(new BoolValue(true), Assert.Contains("approved", variables));
    }

    [Fact]
    public void AnIsoDateIsADate()
    {
        Assert.True(RunInputs.TryParse("when=2024-01-31", out var variables, out _));

        Assert.Equal(new DateValue(new DateOnly(2024, 1, 31)), Assert.Contains("when", variables));
    }

    [Fact]
    public void AnythingElseIsText()
    {
        Assert.True(RunInputs.TryParse("currency=EUR", out var variables, out _));

        Assert.Equal(new StringValue("EUR"), Assert.Contains("currency", variables));
    }

    [Fact]
    public void SeveralPairsAreReadInOneLine()
    {
        Assert.True(RunInputs.TryParse(" amount = 1500 , currency = EUR ", out var variables, out _));

        // The blanks around a pair are the line's, not the value's - a user types them for readability.
        Assert.Equal(new IntValue(1500), Assert.Contains("amount", variables));
        Assert.Equal(new StringValue("EUR"), Assert.Contains("currency", variables));
    }

    [Fact]
    public void NamesAreOrdinal()
    {
        // 'Amount' and 'amount' are two variables, because a name in a document is compared ordinally - reading the
        // line culture-aware would make two names one entry that the document then cannot tell apart.
        Assert.True(RunInputs.TryParse("Amount=1, amount=2", out var variables, out _));

        Assert.Equal(2, variables.Count);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("=1500")]
    [InlineData("amount=")]
    public void AMalformedPairIsRefused(string text)
    {
        Assert.False(RunInputs.TryParse(text, out var variables, out var error));

        Assert.Empty(variables);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ANameGivenTwiceIsRefused()
    {
        Assert.False(RunInputs.TryParse("amount=1, amount=2", out var variables, out var error));

        // The pairs before the mistake are dropped too: running against half the inputs would fail for a reason that
        // is not in the document.
        Assert.Empty(variables);
        Assert.Contains("more than once", error, StringComparison.Ordinal);
    }
}
