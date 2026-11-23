namespace Projectional.Language;

/// <summary>What an operator symbol means for the values this framework holds.</summary>
/// <remarks>
/// <para>
/// A declared evaluation rule can say *which* operator a concept carries and *where* its operands are; it cannot say what
/// the operator does, because that would need a language for computation and this repository has none. So the meaning of
/// the symbols every small vocabulary uses lives here once, and both a declared rule and a hand-written one read it - the
/// alternative is that two languages spelled <c>+</c> two ways and nobody could tell which one a document meant.
/// </para>
/// <para>
/// The set is deliberately closed: whole-number arithmetic (including a power, because <c>x ^ 2</c> is how a function is
/// written), the four comparisons, equality by value, the two logical operators (evaluated eagerly here, because the trace
/// records every operand, where C# short-circuits), the two unary ones, and
/// <c>+</c> over two texts, which joins them. A language that needs more - dates, text *ordering*, which is a decision
/// about a culture rather than about a value, division with a remainder - adds its own rule, which is the point of an
/// aspect being an interface rather than a table.
/// </para>
/// <para>
/// Text is joined here rather than left to a language, because a **declared** rule has no escape hatch: it can name bodies
/// and nothing else, so a label built out of two parts would have been inexpressible. Joining is also the one text operation
/// that needs no decision - it is concatenation, not collation - which is why ordering stays out of this class.
/// </para>
/// <para>
/// Every refusal is a sentence naming the kinds rather than the values, because a trace line is read to find the mistake,
/// and the mistake is almost always the kind.
/// </para>
/// </remarks>
public static class OperatorSemantics
{
    /// <summary>Every binary symbol this applies, in the order a declaration offers them.</summary>
    public static IReadOnlyList<string> Binary { get; } = ["+", "-", "*", "/", "^", "==", "!=", "<", ">", "<=", ">=", "&&", "||"];

    /// <summary>Every unary symbol this applies.</summary>
    public static IReadOnlyList<string> Unary { get; } = ["-", "!"];

    /// <summary>Applies the binary <paramref name="symbol"/> to <paramref name="left"/> and <paramref name="right"/>.</summary>
    /// <param name="context">The rule asking, which turns a refusal into a finding at its own node.</param>
    /// <param name="symbol">The operator, one of <see cref="Binary"/>.</param>
    /// <param name="left">The value on the left.</param>
    /// <param name="right">The value on the right.</param>
    /// <returns>The value the operator produces.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="EvaluationException">The operands are not the kinds the operator wants, or the result is not representable.</exception>
    public static Value Apply(EvaluationContext context, string symbol, Value left, Value right)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(symbol);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (symbol is "&&" or "||")
        {
            return Logic(context, symbol, left, right);
        }

        if (symbol is "==" or "!=")
        {
            return new BoolValue(symbol == "==" ? left.Equals(right) : !left.Equals(right));
        }

        // Before the arithmetic, so that two texts are joined instead of being refused as "not whole numbers": a text that is
        // told it is not a number would send the reader looking for the wrong mistake.
        if (symbol is "+" && left is StringValue first && right is StringValue second)
        {
            return new StringValue(first.Content + second.Content);
        }

        if (symbol is "+" or "-" or "*" or "/" or "^" or "<" or ">" or "<=" or ">=")
        {
            return Arithmetic(context, symbol, left, right);
        }

        throw context.Error($"'{symbol}' is not an operator this framework applies; a language that needs it writes its own rule.");
    }

    /// <summary>Applies the unary <paramref name="symbol"/> to <paramref name="operand"/>.</summary>
    /// <param name="context">The rule asking, which turns a refusal into a finding at its own node.</param>
    /// <param name="symbol">The operator, one of <see cref="Unary"/>.</param>
    /// <param name="operand">The value it applies to.</param>
    /// <returns>The value the operator produces.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="EvaluationException">The operand is not the kind the operator wants.</exception>
    public static Value ApplyUnary(EvaluationContext context, string symbol, Value operand)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(symbol);
        ArgumentNullException.ThrowIfNull(operand);

        return symbol switch
        {
            "-" when operand is IntValue number => new IntValue(-number.Content),
            "-" => throw context.Error(Describe("-", operand)),
            "!" when operand is BoolValue flag => new BoolValue(!flag.Content),
            "!" => throw context.Error(Describe("!", operand)),
            _ => throw context.Error($"'{symbol}' is not a unary operator this framework applies."),
        };
    }

    /// <summary>The two logical symbols, which are about truth values and nothing else.</summary>
    private static BoolValue Logic(EvaluationContext context, string symbol, Value left, Value right)
    {
        if (left is not BoolValue first || right is not BoolValue second)
        {
            throw context.Error(Describe(symbol, left, right));
        }

        return new BoolValue(symbol == "&&" ? first.Content && second.Content : first.Content || second.Content);
    }

    /// <summary>The arithmetic and comparison symbols, which are about whole numbers here.</summary>
    private static Value Arithmetic(EvaluationContext context, string symbol, Value left, Value right)
    {
        if (left is not IntValue first || right is not IntValue second)
        {
            throw context.Error(Describe(symbol, left, right));
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

    /// <summary>Raises a whole number to a whole power, refusing what this framework cannot represent.</summary>
    /// <remarks>
    /// Only whole results: there is no fractional literal in the values this layer holds, and a value nobody can write down
    /// again would be a second kind of number nobody declared.
    /// </remarks>
    private static IntValue Power(EvaluationContext context, int basis, int exponent)
    {
        if (exponent < 0)
        {
            throw context.Error("A power of a whole number needs an exponent of zero or more.");
        }

        var raised = Math.Pow(basis, exponent);

        return raised is > int.MaxValue or < int.MinValue
            ? throw context.Error($"{basis} to the power of {exponent} is beyond the whole numbers this framework holds.")
            : new IntValue((int)raised);
    }

    /// <summary>How an operator complains about what it was given, naming the kinds rather than the values.</summary>
    private static string Describe(string symbol, params Value[] operands) =>
        $"'{symbol}' cannot be applied to {string.Join(" and ", operands.Select(Kind))}.";

    /// <summary>What a value is, in the words a finding uses.</summary>
    private static string Kind(Value value) => value switch
    {
        IntValue => "a whole number",
        BoolValue => "a truth value",
        StringValue => "text",
        DateValue => "a date",
        RecordValue => "a record",
        ListValue => "a list",
        _ => value.GetType().Name,
    };
}
