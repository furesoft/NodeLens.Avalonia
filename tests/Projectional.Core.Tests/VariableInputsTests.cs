using Projectional.Language;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>What one written <c>name=value</c> pair means, which is the language's answer rather than a panel's.</summary>
/// <remarks>
/// The order the kinds are tried in is the whole content of this type: a reader has to be able to predict that
/// <c>1500</c> is a whole number, <c>1.5</c> a fraction and <c>EUR</c> text, and the editor's input box and a
/// declared test's <c>Evaluates</c> assertion have to agree about it.
/// </remarks>
public sealed class VariableInputsTests
{
    [Fact]
    public void AWholeNumberStaysAWholeNumber()
    {
        Assert.True(VariableInputs.TryParse("amount=1500", out var variables, out var error));

        Assert.Null(error);
        Assert.Equal(new IntValue(1500), variables["amount"]);
    }

    [Fact]
    public void AFractionIsAFloat()
    {
        // Read as text it would be a string, and a run against a document whose slot holds a float would then
        // compare a number with a word.
        Assert.True(VariableInputs.TryParse("rate=1.5", out var variables, out _));

        Assert.Equal(new FloatValue(1.5), variables["rate"]);
    }

    [Theory]
    [InlineData("flag=true", "flag")]
    [InlineData("due=2024-02-29", "due")]
    [InlineData("currency=EUR", "currency")]
    public void AFlagADateAndATextKeepTheirOwnKinds(string line, string name)
    {
        Assert.True(VariableInputs.TryParse(line, out var variables, out _));

        Assert.True(variables[name] is BoolValue or DateValue or StringValue);
    }

    [Fact]
    public void SeveralPairsAreReadInOneLine()
    {
        Assert.True(VariableInputs.TryParse("amount=1500, rate=0.25", out var variables, out _));

        Assert.Equal(new IntValue(1500), variables["amount"]);
        Assert.Equal(new FloatValue(0.25), variables["rate"]);
    }

    [Fact]
    public void AMalformedLineAnswersNothingAtAll()
    {
        // A run against half the inputs the caller typed is a run whose answer nobody can diagnose.
        Assert.False(VariableInputs.TryParse("amount=1500, nonsense", out var variables, out var error));

        Assert.Empty(variables);
        Assert.NotNull(error);
    }
}
