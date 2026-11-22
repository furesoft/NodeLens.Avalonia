using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What the expression language means when it is run: literals, arithmetic, comparison, logic, names, and the
/// errors that name the kind an operator could not accept.
/// </summary>
/// <remarks>
/// These are the language's rules, not the runtime's, so they are written the way a reviewer reads the
/// notation: <c>1 + 2</c> is three. Every failure case is here too, because an interpreter that only ever
/// succeeds would be tested by the happy path alone - and the messages are what a user sees in the trace.
/// </remarks>
public sealed class ExpressionsEvaluationTests
{
    [Fact]
    public void LiteralsReadTheirOwnValue()
    {
        var model = NewModel();

        Assert.Equal(new IntValue(42), Run(model, Int(model, 42)));
        Assert.Equal(new BoolValue(true), Run(model, Bool(model, true)));
        Assert.Equal(new StringValue("hello"), Run(model, Text(model, "hello")));
    }

    [Theory]
    [InlineData("+", 9, 4, 13)]
    [InlineData("-", 9, 4, 5)]
    [InlineData("*", 9, 4, 36)]
    [InlineData("/", 8, 4, 2)]
    public void ArithmeticIsAboutWholeNumbers(string symbol, int left, int right, int expected)
    {
        var model = NewModel();

        Assert.Equal(new IntValue(expected), Run(model, Binary(model, symbol, Int(model, left), Int(model, right))));
    }

    [Theory]
    [InlineData(">", 9, 4, true)]
    [InlineData(">", 4, 9, false)]
    [InlineData(">=", 9, 9, true)]
    [InlineData("<", 4, 9, true)]
    [InlineData("<=", 9, 4, false)]
    [InlineData("==", 4, 4, true)]
    [InlineData("!=", 4, 4, false)]
    public void ComparisonsAnswerTruthValues(string symbol, int left, int right, bool expected)
    {
        var model = NewModel();

        Assert.Equal(new BoolValue(expected), Run(model, Binary(model, symbol, Int(model, left), Int(model, right))));
    }

    [Fact]
    public void EqualityComparesValuesOfOneKindByContent()
    {
        var model = NewModel();

        Assert.Equal(
            new BoolValue(true),
            Run(model, Binary(model, "==", Text(model, "a"), Text(model, "a"))));
        Assert.Equal(
            new BoolValue(false),
            Run(model, Binary(model, "==", Text(model, "a"), Text(model, "b"))));
        Assert.Equal(
            new BoolValue(false),
            Run(model, Binary(model, "==", Text(model, "1"), Int(model, 1))));
    }

    [Theory]
    [InlineData("&&", true, false, false)]
    [InlineData("||", true, false, true)]
    public void LogicAnswersTruthValues(string symbol, bool left, bool right, bool expected)
    {
        var model = NewModel();

        Assert.Equal(
            new BoolValue(expected),
            Run(model, Binary(model, symbol, Bool(model, left), Bool(model, right))));
    }
    [Theory]
    [InlineData(2, 3, 8)]
    [InlineData(3, 0, 1)]
    [InlineData(2, 10, 1024)]
    public void APowerRaisesAWholeNumber(int basis, int exponent, int expected)
    {
        var model = NewModel();

        Assert.Equal(
            new IntValue(expected),
            Run(model, Binary(model, "^", Int(model, basis), Int(model, exponent))));
    }

    [Fact]
    public void AUnaryOperatorAppliesToItsOperand()
    {
        var model = NewModel();

        Assert.Equal(new IntValue(-5), Run(model, Unary(model, "-", Int(model, 5))));
        Assert.Equal(new BoolValue(false), Run(model, Unary(model, "!", Bool(model, true))));
    }

    [Fact]
    public void AnIfEvaluatesOnlyTheBranchItChooses()
    {
        var model = NewModel();

        // The unchosen branch is not inserted at all: an empty slot that would fail if it were reached.
        var chosen = model.NewNode(ExpressionsConceptKeys.If);
        chosen.InsertChild("condition", 0, Bool(model, true));
        chosen.InsertChild("then", 0, Int(model, 1));

        var other = model.NewNode(ExpressionsConceptKeys.If);
        other.InsertChild("condition", 0, Bool(model, false));
        other.InsertChild("else", 0, Int(model, 2));

        Assert.Equal(new IntValue(1), Run(model, chosen));
        Assert.Equal(new IntValue(2), Run(model, other));
    }

    [Fact]
    public void AParenthesisedExpressionIsItsInnerOne()
    {
        var model = NewModel();
        var inner = Int(model, 7);
        var parentheses = model.NewNode(ExpressionsConceptKeys.Parenthesized);

        parentheses.InsertChild("inner", 0, inner);

        Assert.Equal(new IntValue(7), Run(model, parentheses));
    }

    [Fact]
    public void ANameReadsTheVariableOfTheRun()
    {
        var model = NewModel();
        var name = model.NewNode(ExpressionsConceptKeys.Name);

        name.Set("name", "amount");

        var result = new Interpreter(ExpressionsEvaluation.Registry).Run(
            name,
            new Dictionary<string, Value>(StringComparer.Ordinal) { ["amount"] = new IntValue(1500) },
            cancellationToken: Token);

        Assert.Equal(new IntValue(1500), result.Value);
    }

    [Fact]
    public void ANameTheRunDoesNotBindIsAFailureNamingIt()
    {
        var model = NewModel();
        var name = model.NewNode(ExpressionsConceptKeys.Name);

        name.Set("name", "amount");

        var result = new Interpreter(ExpressionsEvaluation.Registry).Run(name, cancellationToken: Token);

        Assert.False(result.Succeeded);
        Assert.Contains("amount", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DivisionByZeroIsAFailureRatherThanAnException()
    {
        var model = NewModel();

        var result = RunResult(model, Binary(model, "/", Int(model, 1), Int(model, 0)));

        Assert.False(result.Succeeded);
        Assert.Contains("Division by zero", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOperatorThatCannotTakeItsOperandsSaysWhichKindsTheyAre()
    {
        var model = NewModel();

        var result = RunResult(model, Binary(model, "+", Bool(model, true), Int(model, 1)));

        Assert.False(result.Succeeded);
        Assert.Contains("a truth value", result.Failure!.Message, StringComparison.Ordinal);
        Assert.Contains("a whole number", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOperandThatIsNotThereIsAFailureAboutTheSlot()
    {
        var model = NewModel();
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", "+");
        binary.InsertChild("left", 0, Int(model, 1));

        var result = RunResult(model, binary);

        Assert.False(result.Succeeded);
        Assert.Contains("'right'", result.Failure!.Message, StringComparison.Ordinal);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Model NewModel() => ExpressionsLanguage.NewProject().NewModel("expressions");

    private static Value? Run(Model model, Node node) => RunResult(model, node).Value;

    private static InterpretationResult RunResult(Model model, Node node) =>
        new Interpreter(ExpressionsEvaluation.Registry).Run(node, cancellationToken: Token);

    private static Node Int(Model model, int value)
    {
        var node = model.NewNode(ExpressionsConceptKeys.IntLiteral);
        node.Set("value", value);

        return node;
    }

    private static Node Bool(Model model, bool value)
    {
        var node = model.NewNode(ExpressionsConceptKeys.BoolLiteral);
        node.Set("value", value);

        return node;
    }

    private static Node Text(Model model, string value)
    {
        var node = model.NewNode(ExpressionsConceptKeys.StringLiteral);
        node.Set("value", value);

        return node;
    }

    private static Node Unary(Model model, string symbol, Node operand)
    {
        var node = model.NewNode(ExpressionsConceptKeys.Unary);

        node.Set("operator", symbol);
        node.InsertChild("operand", 0, operand);

        return node;
    }

    private static Node Binary(Model model, string symbol, Node left, Node right)
    {
        var node = model.NewNode(ExpressionsConceptKeys.Binary);

        node.Set("operator", symbol);
        node.InsertChild("left", 0, left);
        node.InsertChild("right", 0, right);

        return node;
    }
}
