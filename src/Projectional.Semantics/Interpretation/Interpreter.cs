using Projectional.Core;
using Projectional.Language;

namespace Projectional.Semantics;

/// <summary>Runs a model's evaluation rules, with a trace, a step budget and cancellation.</summary>
/// <remarks>
/// <para>
/// The interpreter owns everything a rule cannot know: how deep it is, what a child costs, how many steps the
/// run has left, and whether the caller still wants an answer. A rule only says what its own node means, which
/// is what keeps a language's evaluation declarations readable and what makes a rule testable on its own.
/// </para>
/// <para>
/// It is a debugger's runtime rather than an evaluator: the evaluation stack is explicit, every node that
/// produced a value is recorded in post-order, and a runaway model stops at a step budget instead of hanging
/// the window. Nothing is cached - <c>docs/architecture/semantics.md</c> asks for interpretation to be explicit
/// every time, so asking for a child twice costs two steps and shows up twice in the trace.
/// </para>
/// <para>
/// A run is independent of the previous one: <see cref="Run"/> resets the trace, the step count and the
/// variables, so one interpreter can serve a session that runs the same table against different inputs.
/// </para>
/// </remarks>
public sealed class Interpreter : IEvaluationRuntime
{
    /// <summary>How many node evaluations a run may spend unless the caller says otherwise.</summary>
    /// <remarks>
    /// Large enough for a real rule table and small enough that a mistake surfaces in a moment rather than in a
    /// frozen window. A caller that expects more says so per run.
    /// </remarks>
    public const int DefaultBudget = 10_000;

    private static readonly IReadOnlyDictionary<string, Value> NoVariables =
        new Dictionary<string, Value>(StringComparer.Ordinal);

    private readonly EvaluationRegistry _rules;
    private readonly List<TraceEntry> _trace = [];
    private readonly List<Node> _stack = [];

    private IReadOnlyDictionary<string, Value> _variables = NoVariables;
    private CancellationToken _cancellation;
    private InterpretationFailure? _failure;
    private int _steps;
    private int _budget;

    /// <summary>Creates an interpreter that runs <paramref name="rules"/>.</summary>
    /// <param name="rules">The language's evaluation rules.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is <see langword="null"/>.</exception>
    public Interpreter(EvaluationRegistry rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules;
    }

    /// <summary>Evaluates <paramref name="node"/> and answers what it produced, or why it produced nothing.</summary>
    /// <param name="node">The node to run, usually the document's root.</param>
    /// <param name="variables">
    /// The values <c>Name</c> nodes read, or <see langword="null"/> when the model needs none.
    /// </param>
    /// <param name="budget">How many node evaluations the run may spend.</param>
    /// <param name="cancellationToken">Stops the run between two node evaluations.</param>
    /// <returns>The value, the trace and the steps - or the failure that stopped it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="budget"/> is not positive.</exception>
    public InterpretationResult Run(
        Node node,
        IReadOnlyDictionary<string, Value>? variables = null,
        int budget = DefaultBudget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budget, 0);

        _trace.Clear();
        _stack.Clear();
        _failure = null;
        _steps = 0;
        _budget = budget;
        _cancellation = cancellationToken;
        _variables = variables ?? NoVariables;

        Value? value = null;

        try
        {
            value = Evaluate(node);
        }
        catch (EvaluationException error)
        {
            Record(InterpretationFailureKind.Evaluation, error.Message, error.Node);
        }
        catch (EvaluationStoppedException)
        {
            // The failure is already recorded; the sentinel only unwinds the stack.
        }

        return new InterpretationResult(value, _failure, [.. _trace], _steps);
    }

    Value IEvaluationRuntime.Evaluate(Node node) => Evaluate(node);

    Value? IEvaluationRuntime.Variable(string name) =>
        _variables.TryGetValue(name, out var value) ? value : null;

    /// <summary>Evaluates one node: guard, count, run the rule, record the value.</summary>
    /// <remarks>
    /// The record is written after the rule returns, so the trace reads post-order - a child's line precedes the
    /// line of the node that asked for it, which is the order the reader needs to follow the answer back.
    /// </remarks>
    private Value Evaluate(Node node)
    {
        Guard(node);
        _steps++;

        var rule = _rules.RuleFor(node.Concept)
            ?? throw new EvaluationException(
                node,
                $"Concept '{node.Concept.Key}' declares no evaluation rule, so it has no value.");

        var depth = _stack.Count;
        _stack.Add(node);

        try
        {
            var value = rule.Evaluate(new EvaluationContext(node, this));

            _trace.Add(new TraceEntry(depth, node.Id, node.Concept.Key, value));

            return value;
        }
        catch (EvaluationException error)
        {
            // The innermost node wins: an ancestor's catch sees the same exception and must not overwrite the
            // place the failure really happened at, nor add a second line for it.
            if (_failure is null)
            {
                Record(InterpretationFailureKind.Evaluation, error.Message, error.Node);
                _trace.Add(new TraceEntry(depth, node.Id, node.Concept.Key, null));
            }

            throw;
        }
        finally
        {
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    /// <summary>Stops the run before it starts a node, when the caller has cancelled or the budget is spent.</summary>
    private void Guard(Node node)
    {
        if (_cancellation.IsCancellationRequested)
        {
            throw Stop(InterpretationFailureKind.Cancelled, "The run was cancelled.", node);
        }

        if (_steps >= _budget)
        {
            throw Stop(
                InterpretationFailureKind.BudgetExceeded,
                $"The run stopped after {_budget} step(s); the model evaluates more than the budget allows.",
                node);
        }
    }

    private EvaluationStoppedException Stop(InterpretationFailureKind kind, string message, Node node)
    {
        Record(kind, message, node);

        return new EvaluationStoppedException();
    }

    private void Record(InterpretationFailureKind kind, string message, Node node)
    {
        _failure ??= new InterpretationFailure(kind, message, node.Id);
    }
}
