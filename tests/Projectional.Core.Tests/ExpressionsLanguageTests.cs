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
    public void AnExpressionWithoutAnOperatorSaysThatOneIsExpected()
    {
        // A fresh Binary from Enter has no operator at all, and a blank of one space between its operands says
        // nothing about what belongs there. The cell shows the slot it waits for instead.
        var model = ExpressionsLanguage.NewProject().NewModel();
        var sum = model.NewNode(ExpressionsConceptKeys.Binary);
        model.NewChild(sum, "left", ExpressionsConceptKeys.IntLiteral).Set("value", 1);
        model.NewChild(sum, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 2);
        model.AddRoot(sum);

        Assert.Equal("1 <<operator>> 2", LayoutSnapshot.Render(model, Notations, width: 40));
    }

    [Fact]
    public void AUnaryExpressionWithoutAnOperatorSaysThatOneIsExpectedToo()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        var negation = model.NewNode(ExpressionsConceptKeys.Unary);
        model.NewChild(negation, "operand", ExpressionsConceptKeys.Name).Set("name", "amount");
        model.AddRoot(negation);

        Assert.Equal("<<operator>>amount", LayoutSnapshot.Render(model, Notations, width: 40));
    }

    [Fact]
    public void AStringLiteralIsQuotedSoThatItCannotBeMistakenForAName()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        model.AddRoot(model.NewNode(ExpressionsConceptKeys.StringLiteral));
        model.Roots[0].Set("value", "Payroll");

        Assert.Equal("\"Payroll\"", LayoutSnapshot.Render(model, Notations, width: 20));
    }

    [Fact]
    public void TheLanguageShipsTheTypesItsConceptsEvaluateTo()
    {
        // A host that declares a slot over Expression needs "what may stand here" without composing a
        // second package, so the rules belong to the language - the same reason the notation does.
        var model = ExpressionsLanguage.NewProject().NewModel();
        var types = ExpressionsLanguage.Types;

        Assert.Equal("int", types.TypeOf(New(model, ExpressionsConceptKeys.IntLiteral)).Name);
        Assert.Equal("bool", types.TypeOf(New(model, ExpressionsConceptKeys.BoolLiteral)).Name);
        Assert.Equal("string", types.TypeOf(New(model, ExpressionsConceptKeys.StringLiteral)).Name);
    }

    [Fact]
    public void AComparisonIsATruthValueAndArithmeticIsAsWideAsItsLeftOperand()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        var types = ExpressionsLanguage.Types;

        var comparison = model.NewNode(ExpressionsConceptKeys.Binary);
        comparison.Set("operator", "<");
        model.NewChild(comparison, "left", ExpressionsConceptKeys.IntLiteral);
        model.NewChild(comparison, "right", ExpressionsConceptKeys.IntLiteral);

        var sum = model.NewNode(ExpressionsConceptKeys.Binary);
        sum.Set("operator", "+");
        model.NewChild(sum, "left", ExpressionsConceptKeys.StringLiteral);
        model.NewChild(sum, "right", ExpressionsConceptKeys.IntLiteral);

        Assert.Equal("bool", types.TypeOf(comparison).Name);
        Assert.Equal("string", types.TypeOf(sum).Name);
    }

    [Fact]
    public void AWrappedExpressionIsAsWideAsWhatItWraps()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        var parentheses = model.NewNode(ExpressionsConceptKeys.Parenthesized);
        model.NewChild(parentheses, "inner", ExpressionsConceptKeys.BoolLiteral);

        var conditional = model.NewNode(ExpressionsConceptKeys.If);
        model.NewChild(conditional, "then", ExpressionsConceptKeys.IntLiteral);

        Assert.Equal("bool", ExpressionsLanguage.Types.TypeOf(parentheses).Name);
        Assert.Equal("int", ExpressionsLanguage.Types.TypeOf(conditional).Name);
    }

    [Fact]
    public void ANameIsUnknownBecauseWhatItMeansIsDeclaredElsewhere()
    {
        // The sub-language has no declaration to resolve a name against, so it says so instead of
        // guessing - and a host that does have one replaces the rule.
        var model = ExpressionsLanguage.NewProject().NewModel();
        var name = model.NewNode(ExpressionsConceptKeys.Name);

        Assert.Same(UnknownType.Instance, ExpressionsLanguage.Types.TypeOf(name));
    }

    private static Node New(Model model, string conceptKey)
    {
        var node = model.NewNode(conceptKey);

        model.AddRoot(node);

        return node;
    }
}
