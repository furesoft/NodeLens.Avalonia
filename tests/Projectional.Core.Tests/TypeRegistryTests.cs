using Projectional.Core;
using Projectional.Expressions;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The type model: a computed value, assignability asked in one direction, and <c>Unknown</c> as a
/// first-class answer rather than a null.
/// </summary>
public sealed class TypeRegistryTests
{
    [Fact]
    public void AConceptWithNoRuleIsUnknown()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode(ExpressionsConceptKeys.IntLiteral);

        Assert.Same(UnknownType.Instance, TypeRegistry.Empty.TypeOf(literal));
    }

    [Fact]
    public void ADeclaredRuleAnswersForItsConcept()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode(ExpressionsConceptKeys.IntLiteral);

        var registry = TypeRegistry.Empty.Add(ExpressionsConceptKeys.IntLiteral, new Fixed(PropertyType.Integer));

        Assert.Equal("int", registry.TypeOf(literal).Name);
    }

    [Fact]
    public void ARuleIsInheritedByTheConceptsBelowIt()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode(ExpressionsConceptKeys.IntLiteral);

        // Declared on the abstract Expression, asked for a concrete one.
        var registry = TypeRegistry.Empty.Add(ExpressionsConceptKeys.Expression, new Fixed(PropertyType.String));

        Assert.Equal("string", registry.TypeOf(literal).Name);
    }

    [Fact]
    public void APrimitiveTypeAcceptsItsOwnNameAndNothingElse()
    {
        var integer = PrimitiveType.Of(PropertyType.Integer);

        Assert.True(integer.Accepts(PrimitiveType.Of(PropertyType.Integer)));
        Assert.False(integer.Accepts(PrimitiveType.Of(PropertyType.String)));
    }

    [Fact]
    public void UnknownIsAcceptedEverywhereSoOneMistakeDoesNotBecomeFifty()
    {
        Assert.True(PrimitiveType.Of(PropertyType.Integer).Accepts(UnknownType.Instance));
        Assert.True(UnknownType.Instance.Accepts(PrimitiveType.Of(PropertyType.Integer)));
    }

    [Fact]
    public void ARuleCanAskForTheTypeOfItsOwnChild()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var parenthesized = model.NewNode(ExpressionsConceptKeys.Parenthesized);
        var inner = model.NewChild(parenthesized, "inner", ExpressionsConceptKeys.IntLiteral);

        var registry = TypeRegistry.Empty
            .Add(ExpressionsConceptKeys.IntLiteral, new Fixed(PropertyType.Integer))
            .Add(ExpressionsConceptKeys.Parenthesized, new SameAsInner());

        Assert.Equal("int", registry.TypeOf(parenthesized).Name);
        Assert.Equal(inner.Id, parenthesized.GetChildren("inner")[0].Id);
    }

    [Fact]
    public void AnEmptyChildIsUnknownRatherThanAnError()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var parenthesized = model.NewNode(ExpressionsConceptKeys.Parenthesized);

        var registry = TypeRegistry.Empty.Add(ExpressionsConceptKeys.Parenthesized, new SameAsInner());

        // A half-written expression is the ordinary state of a document being edited.
        Assert.Same(UnknownType.Instance, registry.TypeOf(parenthesized));
    }

    [Fact]
    public void AddingAnswersANewRegistryAndLeavesTheOldOneAlone()
    {
        var first = TypeRegistry.Empty.Add(ExpressionsConceptKeys.IntLiteral, new Fixed(PropertyType.Integer));
        var second = first.Add(ExpressionsConceptKeys.BoolLiteral, new Fixed(PropertyType.Boolean));

        Assert.Equal(1, first.Count);
        Assert.Equal(2, second.Count);
    }

    private sealed class Fixed(PropertyType type) : ITypeRule
    {
        public IType TypeFor(TypeContext context) => PrimitiveType.Of(type);
    }

    /// <summary>A parenthesised expression is as wide as what it wraps.</summary>
    private sealed class SameAsInner : ITypeRule
    {
        public IType TypeFor(TypeContext context) => context.TypeOfChild("inner");
    }
}
