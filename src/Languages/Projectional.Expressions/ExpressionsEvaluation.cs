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
        .Add(
            ExpressionsConceptKeys.IntLiteral,
            new DelegateEvaluationRule(context => context.PropertyValue("value"), "The number its value property holds."))
        .Add(
            ExpressionsConceptKeys.BoolLiteral,
            new DelegateEvaluationRule(context => context.PropertyValue("value"), "The truth value its property holds."))
        .Add(
            ExpressionsConceptKeys.StringLiteral,
            new DelegateEvaluationRule(context => context.PropertyValue("value"), "The text its property holds."))
        .Add(
            ExpressionsConceptKeys.Name,
            new DelegateEvaluationRule(ReadName, "The value the run binds to that name."))
        .Add(
            ExpressionsConceptKeys.Parenthesized,
            new DelegateEvaluationRule(context => context.EvalChild("inner"), "What the grouped expression evaluates to."))
        .Add(
            ExpressionsConceptKeys.Unary,
            new DelegateEvaluationRule(ApplyUnary, "Its operator applied to the value of its operand."))
        .Add(
            ExpressionsConceptKeys.Binary,
            new DelegateEvaluationRule(ApplyBinary, "Its operator applied to the values of its two operands."))
        .Add(
            ExpressionsConceptKeys.If,
            new DelegateEvaluationRule(ChooseBranch, "The branch its condition chooses."))
        .Add(
            ExpressionsConceptKeys.Function,
            new DelegateEvaluationRule(context => context.EvalChild("body"), "What its body evaluates to, with its parameters supplied by the run."))
        .Add(
            ExpressionsConceptKeys.Call,
            new DelegateEvaluationRule(ApplyCall, "The body of the function it names, evaluated with the arguments bound."));

    private static Value ReadName(EvaluationContext context) =>
        context.Node.GetOrNull("name") is { Length: > 0 } name
            ? context.Variable(name)
            : throw context.Error("A name must say what it reads.");

    private static Value ChooseBranch(EvaluationContext context) =>
        context.EvalChild("condition") is BoolValue condition
            ? context.EvalChild(condition.Content ? "then" : "else")
            : throw context.Error("The condition of an if must be a truth value.");

    /// <summary>Its operator, applied to the value of its operand by the framework's one meaning for a symbol.</summary>
    /// <remarks>
    /// Read from <see cref="OperatorSemantics"/> rather than repeated here: the two authoring paths are supposed to answer the
    /// same question the same way, and a copy is how they stop doing that. Which operators a document may write is still this
    /// language's business, and that is the notation's enum rather than a check here.
    /// </remarks>
    private static Value ApplyUnary(EvaluationContext context)
    {
        var operand = context.EvalChild("operand");
        var symbol = context.Node.GetOrNull("operator") ?? throw context.Error("A unary expression must say which operator it applies.");

        return OperatorSemantics.ApplyUnary(context, symbol, operand);
    }

    /// <summary>Its operator, applied to the values of its operands by the framework's one meaning for a symbol.</summary>
    private static Value ApplyBinary(EvaluationContext context)
    {
        var left = context.EvalChild("left");
        var right = context.EvalChild("right");
        var symbol = context.Node.GetOrNull("operator") ?? throw context.Error("A binary expression must say which operator it applies.");

        return OperatorSemantics.Apply(context, symbol, left, right);
    }

    /// <summary>Applies the function its callee points at to the values of the arguments.</summary>
    /// <remarks>
    /// The reference is what says which function is meant. A call whose reference does not resolve is a message rather
    /// than a search: a run must not pick a function by name and quietly disagree with the drawing, which resolves to
    /// the node the author actually chose.
    /// </remarks>
    private static Value ApplyCall(EvaluationContext context)
    {
        var model = context.Node.Model;

        if (context.Node.GetReference("callee") is not { } target || !target.IsResolvedIn(model.Project))
        {
            throw context.Error("A call must name a function of this project.");
        }

        var function = model.Roots.FirstOrDefault(root => root.Id.Equals(target.Target))
            ?? throw context.Error("The function this call names is not a document of this model.");

        var callee = function.GetOrNull("name") is { Length: > 0 } named ? named : "a function";

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
}
