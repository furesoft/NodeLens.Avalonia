using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The expression sub-language is what a host borrows rather than writes, so these tests hold it to the
/// two promises that makes: every concept it declares can be projected, and the two concepts a host
/// refers to - the abstract one it declares its slots over and the concrete one the editor falls back
/// to - behave as the host's own concepts do.
/// </summary>
public sealed class ExpressionsLanguageTests
{
    private static NotationRegistry Notations => ExpressionsLanguage.CreateNotations(ExpressionsLanguage.Concepts);

    [Fact]
    public void EveryConcreteConceptCanBeProjectedAndTheAbstractOneCannot()
    {
        var notations = Notations;

        foreach (var concept in ExpressionsLanguage.Concepts.Concepts)
        {
            Assert.Equal(!concept.IsAbstract, notations.TryGet(concept.Key, out _));
        }
    }

    [Fact]
    public void EveryDeclaredConceptThatIsNotTheRootExtendsIt()
    {
        foreach (var concept in ExpressionsLanguage.Concepts.Concepts)
        {
            if (concept.Key == ExpressionsConceptKeys.Expression)
            {
                Assert.True(concept.IsAbstract);
                Assert.Empty(concept.SuperConcepts);
            }
            else
            {
                Assert.True(concept.IsKindOf(ExpressionsConceptKeys.Expression), concept.Key);
            }
        }
    }

    [Fact]
    public void AnEmptyExpressionSlotGetsTheIntegerLiteralBecauseItIsTheFirstConcreteConcept()
    {
        Assert.Equal(
            ExpressionsConceptKeys.IntLiteral,
            ExpressionsLanguage.Concepts.DefaultInstantiableFor(ExpressionsConceptKeys.Expression)?.Key);
    }

    [Fact]
    public void TheOperatorsAreTheEnumerationSymbolsThePropertyStores()
    {
        var binary = ExpressionOperators.BinaryType;
        var unary = ExpressionOperators.UnaryType;

        Assert.All(ExpressionOperators.Binary, symbol =>
        {
            Assert.True(binary.TryParse(symbol, out var stored));
            Assert.Equal(symbol, stored);
        });

        Assert.All(ExpressionOperators.Unary, symbol =>
        {
            Assert.True(unary.TryParse(symbol, out var stored));
            Assert.Equal(symbol, stored);
        });

        Assert.False(binary.TryParse("%", out _));
        Assert.False(unary.TryParse("~", out _));
    }

    [Fact]
    public void ABinaryExpressionReadsLeftOperatorRight()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        var sum = model.NewNode(ExpressionsConceptKeys.Binary);
        sum.Set("operator", "+");
        model.NewChild(sum, "left", ExpressionsConceptKeys.IntLiteral).Set("value", 1);
        model.NewChild(sum, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 2);
        model.AddRoot(sum);

        Assert.Equal("1 + 2", LayoutSnapshot.Render(model, Notations, width: 20));
    }

    [Fact]
    public void GroupingIsVisibleBecauseTheTreeCannotBeReadBackOutOfTheText()
    {
        // The tree groups (1 + 2) * 3. Rendering it as 1 + 2 * 3 would be the same tree only if the
        // reader happened to know the precedence, which a projection may not assume.
        var model = ExpressionsLanguage.NewProject().NewModel();
        var product = model.NewNode(ExpressionsConceptKeys.Binary);
        product.Set("operator", "*");

        var sum = model.NewChild(product, "left", ExpressionsConceptKeys.Parenthesized);
        var inner = model.NewChild(sum, "inner", ExpressionsConceptKeys.Binary);
        inner.Set("operator", "+");
        model.NewChild(inner, "left", ExpressionsConceptKeys.IntLiteral).Set("value", 1);
        model.NewChild(inner, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 2);

        model.NewChild(product, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 3);
        model.AddRoot(product);

        Assert.Equal("(1 + 2) * 3", LayoutSnapshot.Render(model, Notations, width: 20));
    }

    [Fact]
    public void AUnaryExpressionDrawsItsOperatorInFrontOfItsOperand()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        var negation = model.NewNode(ExpressionsConceptKeys.Unary);
        negation.Set("operator", "-");
        model.NewChild(negation, "operand", ExpressionsConceptKeys.Name).Set("name", "amount");
        model.AddRoot(negation);

        Assert.Equal("-amount", LayoutSnapshot.Render(model, Notations, width: 20));
    }

    [Fact]
    public void AStringLiteralIsQuotedSoThatItCannotBeMistakenForAName()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        model.AddRoot(model.NewNode(ExpressionsConceptKeys.StringLiteral));
        model.Roots[0].Set("value", "Payroll");

        Assert.Equal("\"Payroll\"", LayoutSnapshot.Render(model, Notations, width: 20));
    }
}
