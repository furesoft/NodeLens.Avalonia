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

    [Fact]
    public void PlusJoinsTwoTextsAndOnlyTexts()
    {
        // The one text operation that needs no cultural decision, and the one a declared rule could not otherwise express.
        var model = NewModel();

        Assert.Equal(new StringValue("ab"), Run(model, Binary(model, "+", Text(model, "a"), Text(model, "b"))));

        // A text and a number still refuse rather than turn the number into text: how a number reads is a formatting decision.
        var mixed = RunResult(model, Binary(model, "+", Text(model, "a"), Int(model, 1)));

        Assert.Equal("'+' cannot be applied to text and a whole number.", mixed.Failure?.Message);
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

    [Fact]
    public void TheLogicalOperatorsShortCircuitSoTheRightOperandIsNeverEvaluated()
    {
        // `true || (1 / 0)` has no value on the right, and a short-circuiting `||` never asks for it. That is what C#
        // does, and what makes a rule mean the same thing in the editor and in the code it generates.
        var model = NewModel();

        Assert.Equal(
            new BoolValue(true),
            Run(model, Binary(model, "||", Bool(model, true), Binary(model, "/", Int(model, 1), Int(model, 0)))));
        Assert.Equal(
            new BoolValue(false),
            Run(model, Binary(model, "&&", Bool(model, false), Binary(model, "/", Int(model, 1), Int(model, 0)))));
    }

    [Fact]
    public void AnOperandTheShortCircuitDoesNotSkipIsStillEvaluated()
    {
        // The mirror: when the left operand decides nothing, the right one runs and its failure is the run's failure -
        // a short-circuit skips an operand rather than swallowing a mistake.
        var model = NewModel();

        var result = RunResult(model, Binary(model, "||", Bool(model, false), Binary(model, "/", Int(model, 1), Int(model, 0))));

        Assert.False(result.Succeeded);
        Assert.Equal("Division by zero has no value.", result.Failure?.Message);
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
    public void ACallAppliesTheFunctionItNames()
    {
        // `f(x) = x ^ 2` applied to 4: the definition is a document of the model, the call resolves its name among those
        // documents, and the arguments are bound for the body - which is what the evaluation scope was added for.
        var model = NewModel();
        var square = Function(model, "square", ["x"], Binary(model, "^", Name(model, "x"), Int(model, 2)));
        var call = Call(model, square, Int(model, 4));

        Assert.Equal(new IntValue(16), Run(model, call));
    }

    [Fact]
    public void ACallIsRefusedWhenItNamesNothingOrBringsTheWrongCount()
    {
        var model = NewModel();
        var square = Function(model, "square", ["x"], Binary(model, "^", Name(model, "x"), Int(model, 2)));

        // A call to a name no document declares is a message rather than a guess, and the count is compared because a
        // mismatch would otherwise surface as "the parameter has no value" two nodes deeper than the mistake.
        Assert.NotNull(RunResult(model, DanglingCall(model, "cube", Int(model, 2))).Failure);
        Assert.NotNull(RunResult(model, Call(model, square, Int(model, 2), Int(model, 3))).Failure);
    }
    [Fact]
    public void AScopeBindsANameForTheNodeItEncloses()
    {
        var model = NewModel();
        var name = model.NewNode(ExpressionsConceptKeys.Name);
        name.Set("name", "x");

        var runtime = new Interpreter(ExpressionsEvaluation.Registry);
        var outer = new EvaluationContext(name, runtime);

        // Nothing is bound at the top of a run, so the name has no value there.
        Assert.Throws<EvaluationException>(() => outer.Variable("x"));

        // A call binds its arguments in front of the enclosing scope, and the binding is a value rather than a stack that
        // is pushed and popped: the context it was made from is unchanged, so a rule cannot leak one call's parameters
        // into the next one by forgetting to pop.
        var inner = outer.With(new Dictionary<string, Value>(StringComparer.Ordinal) { ["x"] = new IntValue(2) });

        Assert.Equal(new IntValue(2), inner.Variable("x"));
        Assert.Throws<EvaluationException>(() => outer.Variable("x"));
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

    private static Node Name(Model model, string name)
    {
        var node = model.NewNode(ExpressionsConceptKeys.Name);
        node.Set("name", name);

        return node;
    }

    private static Node Function(Model model, string name, string[] parameters, Node body)
    {
        var node = model.NewNode(ExpressionsConceptKeys.Function);
        node.Set("name", name);

        // A parameter is no concept that may start a document, so it is created as the child it is.
        foreach (var each in parameters)
        {
            var parameter = model.NewChild(node, ExpressionsLanguage.ParametersSlot, ExpressionsConceptKeys.Parameter);
            parameter.Set("name", each);
        }

        node.InsertChild("body", 0, body);

        return node;
    }

    private static Node Call(Model model, Node function, params Node[] arguments)
    {
        var node = model.NewNode(ExpressionsConceptKeys.Call);
        node.SetReference("callee", function);

        for (var index = 0; index < arguments.Length; index++)
        {
            node.InsertChild(ExpressionsLanguage.ArgumentsSlot, index, arguments[index]);
        }

        return node;
    }

    /// <summary>A call whose callee keeps the text it was given and resolves to nothing.</summary>
    private static Node DanglingCall(Model model, string callee, params Node[] arguments)
    {
        var node = model.NewNode(ExpressionsConceptKeys.Call);
        node.SetReference("callee", NodeId.New(), callee);

        for (var index = 0; index < arguments.Length; index++)
        {
            node.InsertChild(ExpressionsLanguage.ArgumentsSlot, index, arguments[index]);
        }

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
