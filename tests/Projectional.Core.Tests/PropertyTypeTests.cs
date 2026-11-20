using Xunit;

namespace Projectional.Core.Tests;

public sealed class PropertyTypeTests
{
    [Fact]
    public void PrimitiveTypesAreSingletonsWithCanonicalNames()
    {
        Assert.Same(PropertyType.String, PropertyType.String);
        Assert.Equal("string", PropertyType.String.Name);
        Assert.Equal("int", PropertyType.Integer.Name);
        Assert.Equal("float", PropertyType.Float.Name);
        Assert.Equal("bool", PropertyType.Boolean.Name);
        Assert.Equal("date", PropertyType.Date.Name);
    }

    [Fact]
    public void DifferentPrimitiveTypesAreNotEqual()
    {
        Assert.NotEqual(PropertyType.String, PropertyType.Integer);
        Assert.NotEqual(PropertyType.Integer, PropertyType.Boolean);
        Assert.NotEqual(PropertyType.Integer, PropertyType.Float);
        Assert.Equal(PropertyType.Integer, PropertyType.Integer);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1.5", 1.5)]
    [InlineData("-0.25", -0.25)]
    [InlineData("1e3", 1000)]
    [InlineData("0.1", 0.1)]
    public void FloatsParseInTheInvariantCulture(string text, double expected)
    {
        Assert.True(PropertyType.Float.TryParse(text, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void FloatsAreStoredInTheShortestSpellingThatReadsBack()
    {
        // What the writer puts in the file: one spelling per value, and it round-trips, so a save after a load
        // cannot change the text.
        Assert.Equal("1.5", PropertyType.Float.Format(1.5));
        Assert.Equal("0.1", PropertyType.Float.Format(0.1));
        Assert.Equal("1", PropertyType.Float.Format(1.0));

        Assert.True(PropertyType.Float.TryParse(PropertyType.Float.Format(0.1), out var value));
        Assert.Equal(0.1, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1,5")]
    [InlineData("half")]
    [InlineData("0x10")]
    public void ValuesOutsideTheFloatTypeAreRejected(string text)
    {
        // The infinities and NaN parse, and they are refused anyway: a number no comparison can answer is a
        // mistake wherever it is stored rather than a value a slot should be able to hold.
        Assert.False(PropertyType.Float.TryParse(text, out _));
        Assert.False(PropertyType.Float.IsValid(text));
    }

    [Fact]
    public void FormattingANonFiniteOrForeignValueIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PropertyType.Float.Format(double.NaN));
        Assert.Throws<ArgumentException>(() => PropertyType.Float.Format(double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => PropertyType.Float.Format(1));
    }

    [Fact]
    public void StringAcceptsAnyTextAndTreatsNullAsEmpty()
    {
        Assert.True(PropertyType.String.TryParse("anything at all", out var value));
        Assert.Equal("anything at all", value);

        Assert.True(PropertyType.String.TryParse(null, out var empty));
        Assert.Equal(string.Empty, empty);

        Assert.Equal("x", PropertyType.String.Format("x"));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-17", -17)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void IntegersParseInTheInvariantCulture(string text, int expected)
    {
        Assert.True(PropertyType.Integer.TryParse(text, out var value));
        Assert.Equal(expected, value);
        Assert.Equal(text, PropertyType.Integer.Format(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("0x10")]
    [InlineData("ten")]
    public void ValuesOutsideTheIntegerTypeAreRejected(string text)
    {
        Assert.False(PropertyType.Integer.TryParse(text, out _));
        Assert.False(PropertyType.Integer.IsValid(text));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void BooleansParseAndFormatCanonically(string text, bool expected)
    {
        Assert.True(PropertyType.Boolean.TryParse(text, out var value));
        Assert.Equal(expected, value);
        Assert.Equal(text, PropertyType.Boolean.Format(expected));
    }

    [Fact]
    public void BooleansFormatLowercaseRegardlessOfInputSpelling()
    {
        Assert.True(PropertyType.Boolean.TryParse("True", out _));
        Assert.Equal("true", PropertyType.Boolean.Format(true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("yes")]
    public void NonBooleanTextIsRejected(string text) => Assert.False(PropertyType.Boolean.IsValid(text));

    [Fact]
    public void DatesUseTheSingleCanonicalPattern()
    {
        Assert.True(PropertyType.Date.TryParse("2024-02-29", out var value));
        Assert.Equal(new DateOnly(2024, 2, 29), value);
        Assert.Equal("2024-02-29", PropertyType.Date.Format(new DateOnly(2024, 2, 29)));
    }

    [Theory]
    [InlineData("2024-2-9")]
    [InlineData("29.02.2024")]
    [InlineData("2024-02-29T00:00:00")]
    [InlineData("2023-02-29")]
    public void AnythingButTheCanonicalPatternIsRejected(string text) => Assert.False(PropertyType.Date.IsValid(text));

    [Fact]
    public void EnumerationsAcceptOnlyTheirOwnSymbols()
    {
        var status = PropertyType.Enum("draft", "approved", "rejected");

        Assert.True(status.TryParse("approved", out var value));
        Assert.Equal("approved", value);
        Assert.Equal("approved", status.Format("approved"));
        Assert.False(status.TryParse("APPROVED", out _));
        Assert.False(status.TryParse("unknown", out _));
    }

    [Fact]
    public void EnumerationNamesListTheSymbolsInDeclarationOrder()
    {
        Assert.Equal("enum(draft, approved)", PropertyType.Enum("draft", "approved").Name);
    }

    [Fact]
    public void EnumerationsCompareByTheirSymbols()
    {
        Assert.Equal(PropertyType.Enum("a", "b"), PropertyType.Enum("a", "b"));
        Assert.NotEqual(PropertyType.Enum("a", "b"), PropertyType.Enum("b", "a"));
        Assert.NotEqual(PropertyType.Enum("a"), PropertyType.String);
    }

    [Fact]
    public void AnEmptyOrDuplicateEnumerationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PropertyType.Enum());
        Assert.Throws<ArgumentException>(() => PropertyType.Enum("a", "a"));
        Assert.Throws<ArgumentException>(() => PropertyType.Enum("a", ""));
    }

    [Fact]
    public void FormattingAValueOfTheWrongTypeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PropertyType.Integer.Format("nope"));
        Assert.Throws<ArgumentException>(() => PropertyType.Date.Format(42));
        Assert.Throws<ArgumentException>(() => PropertyType.Enum("a").Format("b"));
    }
}
