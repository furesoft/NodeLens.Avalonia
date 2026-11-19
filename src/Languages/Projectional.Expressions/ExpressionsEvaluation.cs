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
        .Add(ExpressionsConceptKeys.If, new DelegateEvaluationRule(ChooseBranch));

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
            "+" or "-" or "*" or "/" or "<" or ">" or "<=" or ">=" => Arithmetic(context, left, right),
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
            "<" => new BoolValue(first.Content < second.Content),
            ">" => new BoolValue(first.Content > second.Content),
            "<=" => new BoolValue(first.Content <= second.Content),
            _ => new BoolValue(first.Content >= second.Content),
        };
    }

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
