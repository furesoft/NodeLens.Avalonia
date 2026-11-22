namespace Projectional.Language;

/// <summary>An evaluation rule written as a function, which is how most rules are meant.</summary>
/// <remarks>
/// A language declares what each concept means; most of those meanings are one expression long, and a class per
/// concept would bury the declaration in ceremony. The wrapper is a type rather than an interface implemented
/// by a lambda because C# has no lambda-to-interface conversion, and the alternative - seven tiny classes in a
/// language package - is what this avoids.
/// </remarks>
/// <param name="evaluate">What the node evaluates to.</param>
public sealed class DelegateEvaluationRule(Func<EvaluationContext, Value> evaluate, string? description = null) : IEvaluationRule
{
    private readonly Func<EvaluationContext, Value> _evaluate =
        evaluate ?? throw new ArgumentNullException(nameof(evaluate));

    /// <inheritdoc />
    public Value Evaluate(EvaluationContext context) => _evaluate(context);

    public string? Description => description;
}
