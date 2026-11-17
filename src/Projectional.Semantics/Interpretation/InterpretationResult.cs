using Projectional.Language;

namespace Projectional.Semantics;

/// <summary>What one run of the interpreter produced: a value, or why it produced none.</summary>
/// <remarks>
/// Exactly one of <see cref="Value"/> and <see cref="Failure"/> is set. The trace is present in both cases,
/// because "it failed at this node after these steps" is the more useful answer when something went wrong.
/// </remarks>
public sealed class InterpretationResult
{
    internal InterpretationResult(
        Value? value,
        InterpretationFailure? failure,
        IReadOnlyList<TraceEntry> trace,
        int steps)
    {
        Value = value;
        Failure = failure;
        Trace = trace;
        Steps = steps;
    }

    /// <summary>What the root evaluated to, or <see langword="null"/> when the run failed.</summary>
    public Value? Value { get; }

    /// <summary>Why the run stopped without a value, or <see langword="null"/> when it succeeded.</summary>
    public InterpretationFailure? Failure { get; }

    /// <summary>Every node that was evaluated, in the order the values were finished.</summary>
    public IReadOnlyList<TraceEntry> Trace { get; }

    /// <summary>How many node evaluations the run cost.</summary>
    public int Steps { get; }

    /// <summary>Whether the run produced a value.</summary>
    public bool Succeeded => Failure is null;

    public override string ToString() =>
        Succeeded ? $"{Value!.Display} ({Steps} step(s))" : Failure!.ToString();
}
