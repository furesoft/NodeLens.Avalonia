using Projectional.Core;

namespace Projectional.Language;

/// <summary>The two questions a rule may ask the runtime that evaluates it.</summary>
/// <remarks>
/// <para>
/// It is an interface in the language layer and an implementation in the semantics layer, for the same reason
/// <see cref="EvaluationContext"/> is a class here: a rule must be able to evaluate a child without knowing
/// who runs it. The runtime owns the budget, the stack and the trace, so a rule that asks twice for the same
/// child costs two steps and appears twice - there is no caching to hide behind
/// (<c>docs/architecture/semantics.md</c>: interpretation is always explicit, never cached).
/// </para>
/// <para>
/// This interface is not part of a language's declaration surface. A language author writes
/// <see cref="IEvaluationRule"/>s; only this framework implements the runtime.
/// </para>
/// </remarks>
public interface IEvaluationRuntime
{
    /// <summary>What <paramref name="node"/> evaluates to, counted and recorded by the runtime.</summary>
    /// <param name="node">The node to evaluate.</param>
    /// <param name="locals">
    /// The names bound in the scope the node stands in, which the runtime hands on to the context of the rule it runs -
    /// empty at the top of a run, and a call parameters below one.
    /// </param>
    /// <exception cref="EvaluationException">The node cannot be evaluated, or the run must stop.</exception>
    Value Evaluate(Node node, IReadOnlyDictionary<string, Value> locals);

    /// <summary>The value bound to the variable <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <param name="name">The name to look up.</param>
    Value? Variable(string name);
}
