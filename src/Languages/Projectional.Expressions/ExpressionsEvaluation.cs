using Projectional.Core;
using Projectional.Language;

namespace Projectional.Expressions;

/// <summary>What the expression concepts evaluate to.</summary>
/// <remarks>
/// <para>
/// This is the language's meaning, declared beside its concepts, notation, typing rules and types - not a
/// framework opinion about expressions. A literal reads its own property; a name reads a variable of the run;
/// an operator applies itself to the values of its operands; a parenthesised expression is its inner one.
/// </para>
/// <para>
/// Every failure is a message a user can act on, and every one of them is a case the type rules also describe:
/// the interpreter checks anyway, because a document being edited can have an <c>Unknown</c> type and a run
/// must not trust a type nobody could compute. Precedence does not appear here for the same reason it does not
/// appear in the notation: the tree already says what it means.
/// </para>
/// </remarks>
public static class ExpressionsEvaluation
{
    /// <summary>The rules of <see cref="ExpressionsLanguage"/>, composed the way its types are.</summary>
    public static EvaluationRegistry Registry { get; } = Create();

    private static EvaluationRegistry Create() => EvaluationRegistry.Empty
        .Add(ExpressionsConceptKeys.IntLiteral, new DelegateEvaluationRule(context => context.PropertyValue("value")))
        .Add(ExpressionsConceptKeys.BoolLiteral, new DelegateEvaluationRule(context => context.PropertyValue("value")))
        .Add(ExpressionsConceptKeys.StringLiteral, new DelegateEvaluationRule(context => context.PropertyValue("value")))
        .Add(ExpressionsConceptKeys.Name, new DelegateEvaluationRule(ReadName))
        .Add(ExpressionsConceptKeys.Parenthesized, new DelegateEvaluationRule(context => context.EvalChild("inner")))
        .Add(ExpressionsConceptKeys.Unary, new DelegateEvaluationRule(ApplyUnary))
        .Add(ExpressionsConceptKeys.Binary, new DelegateEvaluationRule(ApplyBinary))
        .Add(ExpressionsConceptKeys.If, new DelegateEvaluationRule(ChooseBranch))
        .Add(ExpressionsConceptKeys.Function, new DelegateEvaluationRule(context => context.EvalChild("body")))
        .Add(ExpressionsConceptKeys.Call, new DelegateEvaluationRule(ApplyCall));

    private static Value ReadName(EvaluationContext context) =>
        context.Node.GetOrNull("name") is { Length: > 0 } name
            ? context.Variable(name)
            : throw context.Error("A name must say what it reads.");

    private static Value ChooseBranch(EvaluationContext context) =>
        context.EvalChild("condition") is BoolValue condition
            ? context.EvalChild(condition.Content ? "then" : "else")
            : throw context.Error("The condition of an if must be a truth value.");

    private static Value ApplyUnary(EvaluationContext context)
    {
        var operand = context.EvalChild("operand");

        return context.Node.GetOrNull("operator") switch
        {
            "-" when operand is IntValue number => new IntValue(-number.Content),
            "-" => throw context.Error($"{Describe("-", operand)} expects a whole number."),
            "!" when operand is BoolValue flag => new BoolValue(!flag.Content),
            "!" => throw context.Error($"{Describe("!", operand)} expects a truth value."),
            var other => throw context.Error($"'{other}' is not an operator of a unary expression."),
        };
    }

    private static Value ApplyBinary(EvaluationContext context)
    {
        var left = context.EvalChild("left");
        var right = context.EvalChild("right");

        return context.Node.GetOrNull("operator") switch
        {
            "+" or "-" or "*" or "/" or "^" or "<" or ">" or "<=" or ">=" => Arithmetic(context, left, right),
            "==" => new BoolValue(Equals(left, right)),
            "!=" => new BoolValue(!Equals(left, right)),
            "&&" => Logic(context, left, right, and: true),
            "||" => Logic(context, left, right, and: false),
            var other => throw context.Error($"'{other}' is not an operator of a binary expression."),
        };
    }

    /// <summary>Applies the comparison and arithmetic operators, which are about whole numbers here.</summary>
    /// <remarks>
    /// A language that wants dates or text ordered adds its own concepts (or its own rules) rather than making
    /// this one guess: "what does <c>&lt;</c> mean for a string" is a decision the language makes, and this
    /// language has no opinion.
    /// </remarks>
    private static Value Arithmetic(EvaluationContext context, Value left, Value right)
    {
        var symbol = context.Node.GetOrNull("operator")!;

        if (left is not IntValue first || right is not IntValue second)
        {
            throw context.Error($"{Describe(symbol, left, right)} expects whole numbers.");
        }

        return symbol switch
        {
            "+" => new IntValue(first.Content + second.Content),
            "-" => new IntValue(first.Content - second.Content),
            "*" => new IntValue(first.Content * second.Content),
            "/" when second.Content == 0 => throw context.Error("Division by zero has no value."),
            "/" => new IntValue(first.Content / second.Content),
            "^" => Power(context, first.Content, second.Content),
            "<" => new BoolValue(first.Content < second.Content),
            ">" => new BoolValue(first.Content > second.Content),
            "<=" => new BoolValue(first.Content <= second.Content),
            _ => new BoolValue(first.Content >= second.Content),
        };
    }

    /// <summary>Raises a whole number to a whole power, refusing what this language cannot represent.</summary>
    /// <remarks>
    /// Only whole results, because the vocabulary has no fractional literal to write one in and a value nobody can
    /// spell again would be a second kind of number the language does not admit to having.
    /// </remarks>
    private static IntValue Power(EvaluationContext context, int basis, int exponent)
    {
        if (exponent < 0)
        {
            throw context.Error("A power of a whole number needs an exponent of zero or more.");
        }

        var raised = Math.Pow(basis, exponent);

        return raised is > int.MaxValue or < int.MinValue
            ? throw context.Error($"{basis} to the power of {exponent} is beyond the whole numbers this language holds.")
            : new IntValue((int)raised);
    }

    /// <summary>Applies a function of the model to the values of the arguments.</summary>
    /// <remarks>
    /// The callee is resolved among the documents of the model, by name, which is this vocabulary's own doctrine: a name
    /// is what it means to whoever runs the model, and no reference slot has to exist for it. Two functions of one name
    /// are therefore ambiguous rather than shadowed - the first in document order answers, and the constraint that would
    /// report the pair is the language's next step rather than the interpreter's guess.
    /// </remarks>
    private static Value ApplyCall(EvaluationContext context)
    {
        var callee = context.Node.GetOrNull("callee") is { Length: > 0 } name
            ? name
            : throw context.Error("A call must say which function it applies.");

        var function = FunctionNamed(context, callee)
            ?? throw context.Error($"No function of this model is called '{callee}'.");

        var parameters = function.GetChildren(ExpressionsLanguage.ParametersSlot);
        var arguments = context.Node.GetChildren(ExpressionsLanguage.ArgumentsSlot);

        if (parameters.Count != arguments.Count)
        {
            throw context.Error(
                $"'{callee}' takes {parameters.Count} argument(s), but {arguments.Count} were given.");
        }

        Dictionary<string, Value> bound = new(StringComparer.Ordinal);

        for (var index = 0; index < parameters.Count; index++)
        {
            if (parameters[index].GetOrNull("name") is not { Length: > 0 } parameter)
            {
                throw context.Error($"Parameter {index + 1} of '{callee}' has no name.");
            }

            if (!bound.TryAdd(parameter, context.Eval(arguments[index])))
            {
                throw context.Error($"'{callee}' declares '{parameter}' twice.");
            }
        }

        // The body belongs to the function rather than to the call, so the node that is evaluated is the one the
        // definition holds - inside the scope the arguments built.
        var body = function.GetChild("body")
            ?? throw context.Error($"function {callee} declares no body.");

        return context.With(bound).Eval(body);
    }

    private static Node? FunctionNamed(EvaluationContext context, string name) =>
        context.Node.Model.Roots
            .Where(root => root.Concept.IsKindOf(ExpressionsConceptKeys.Function))
            .FirstOrDefault(root => string.Equals(root.GetOrNull("name"), name, StringComparison.Ordinal));

    private static BoolValue Logic(EvaluationContext context, Value left, Value right, bool and)
    {
        if (left is not BoolValue first || right is not BoolValue second)
        {
            throw context.Error($"{Describe(and ? "&&" : "||", left, right)} expects truth values.");
        }

        return new BoolValue(and ? first.Content && second.Content : first.Content || second.Content);
    }

    /// <summary>How an operator complains about what it was given, naming the kinds rather than the values.</summary>
    private static string Describe(string symbol, params Value[] operands) =>
        $"'{symbol}' cannot be applied to {string.Join(" and ", operands.Select(operand => Kind(operand)))}.";

    private static string Kind(Value value) => value switch
    {
        IntValue => "a whole number",
        BoolValue => "a truth value",
        StringValue => "text",
        DateValue => "a date",
        _ => value.GetType().Name,
    };
}
