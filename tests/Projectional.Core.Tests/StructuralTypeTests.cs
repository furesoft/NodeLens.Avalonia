using Projectional.Core;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The structural types: a record is a set of named fields, a list is its element, and an optional is a
/// promise about absence.
/// </summary>
public sealed class StructuralTypeTests
{
    private static readonly PrimitiveType Int = PrimitiveType.Of(PropertyType.Integer);
    private static readonly PrimitiveType Text = PrimitiveType.Of(PropertyType.String);

    [Fact]
    public void ARecordIsTheSameTypeWhenItsFieldsAgree()
    {
        var left = new RecordType([("amount", Int), ("currency", Text)]);
        var right = new RecordType([("amount", Int), ("currency", Text)]);

        Assert.True(left.Accepts(right));
        Assert.Equal("(amount: int, currency: string)", left.Name);
    }

    [Fact]
    public void ARecordComparesByNameNotByOrder()
    {
        var left = new RecordType([("amount", Int), ("currency", Text)]);
        var right = new RecordType([("currency", Text), ("amount", Int)]);

        Assert.True(left.Accepts(right));
    }

    [Fact]
    public void ARecordWithADifferentFieldIsADifferentType()
    {
        var left = new RecordType([("amount", Int)]);
        var right = new RecordType([("amount", Int), ("currency", Text)]);

        Assert.False(left.Accepts(right));
    }

    [Fact]
    public void ARecordWithAFieldOfTheWrongTypeIsADifferentType()
    {
        var left = new RecordType([("amount", Int)]);
        var right = new RecordType([("amount", Text)]);

        Assert.False(left.Accepts(right));
    }

    [Fact]
    public void ARecordRefusesOneFieldDeclaredTwice()
    {
        Assert.Throws<ArgumentException>(() => new RecordType([("amount", Int), ("amount", Text)]));
    }

    [Fact]
    public void AListIsItsElement()
    {
        var ints = new ListType(Int);

        Assert.True(ints.Accepts(new ListType(Int)));
        Assert.False(ints.Accepts(new ListType(Text)));
        Assert.False(ints.Accepts(Int));
        Assert.Equal("[int]", ints.Name);
    }

    [Fact]
    public void AnOptionalAcceptsItsInnerTypeAndAnotherOptional()
    {
        var optional = new OptionalType(Int);

        Assert.True(optional.Accepts(Int));
        Assert.True(optional.Accepts(new OptionalType(Int)));
        Assert.False(optional.Accepts(Text));
        Assert.Equal("int?", optional.Name);
    }

    [Fact]
    public void APlainTypeDoesNotAcceptAnOptional()
    {
        // A slot that promised a value would be getting one that might not be there.
        Assert.False(Int.Accepts(new OptionalType(Int)));
    }

    [Fact]
    public void NestingIsNotCollapsed()
    {
        var once = new OptionalType(Int);
        var twice = new OptionalType(once);

        Assert.False(once.Accepts(twice));
        Assert.Equal("int??", twice.Name);
    }

    [Fact]
    public void EveryStructuralTypeAcceptsUnknown()
    {
        // One mistake must not become a finding on every expression that reads the value.
        Assert.True(new RecordType([("amount", Int)]).Accepts(UnknownType.Instance));
        Assert.True(new ListType(Int).Accepts(UnknownType.Instance));
        Assert.True(new OptionalType(Int).Accepts(UnknownType.Instance));
    }

    [Fact]
    public void AStructuralTypeCanBeNestedInAnother()
    {
        var order = new RecordType([("lines", new ListType(new RecordType([("amount", Int)])))]);

        Assert.True(order.Accepts(new RecordType([("lines", new ListType(new RecordType([("amount", Int)])))])));
        Assert.Equal("(lines: [(amount: int)])", order.Name);
    }
}
