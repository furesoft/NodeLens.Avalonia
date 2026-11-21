using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>A place in a finished run's trace: where a stepping reader stands.</summary>
/// <remarks>
/// <para>
/// Stepping is a <em>replay</em>, not a paused interpreter. The runtime already records every node it evaluated and
/// what came out, in the order the values were finished, so there is nothing to suspend and nothing to resume: the
/// cursor walks a list that a completed run left behind. That is why the interpreter did not change to gain stepping,
/// and why a step cannot have a side effect.
/// </para>
/// <para>
/// The order is <b>post-order</b>, so a child's value is shown before the parent's that consumes it - a cascade of
/// finished values rather than an enter/exit trace of a call stack. That is the order the trace was recorded in and
/// the order a reader follows "why is this value what it is"; a tracer that also shows a node being <em>entered</em>
/// would need the interpreter to record a second event, which is a different feature and not this one.
/// </para>
/// <para>
/// The cursor starts <em>before</em> the first step, at <see cref="Position"/> <c>-1</c>, because a reader who has just
/// run a document stands at the beginning of the story rather than on its first line. That also makes "step" mean one
/// thing at every moment: it advances the cursor and answers whether it could, so a shell can drive a button from the
/// answer without a second test.
/// </para>
/// </remarks>
public sealed class TraceCursor
{
    private readonly InterpretationResult _result;

    /// <summary>Creates a cursor standing before the first step of <paramref name="result"/>.</summary>
    /// <param name="result">The run to replay; it needs no value, only its trace.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public TraceCursor(InterpretationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _result = result;
        Position = -1;
    }

    /// <summary>The run being replayed.</summary>
    public InterpretationResult Result => _result;

    /// <summary>How many nodes the run evaluated, which is how many steps there are to take.</summary>
    public int Count => _result.Trace.Count;

    /// <summary>Where the cursor stands, or <c>-1</c> before the first step.</summary>
    public int Position { get; private set; }

    /// <summary>The step the cursor stands on, or <see langword="null"/> before the first one.</summary>
    public TraceEntry? Current => Position >= 0 && Position < Count ? _result.Trace[Position] : null;

    /// <summary>Whether the cursor stands before the first step.</summary>
    public bool IsAtStart => Position < 0;

    /// <summary>Whether the cursor stands on the last step, which is also true of a run with no steps.</summary>
    public bool IsAtEnd => Position >= Count - 1;

    /// <summary>Moves to the next step.</summary>
    /// <returns><see langword="true"/> when the cursor moved, <see langword="false"/> when it was already at the end.</returns>
    public bool Step()
    {
        if (IsAtEnd)
        {
            return false;
        }

        Position++;
        return true;
    }

    /// <summary>Moves to the previous step, or back to before the first one.</summary>
    /// <returns><see langword="true"/> when the cursor moved, <see langword="false"/> when it was already at the start.</returns>
    public bool Back()
    {
        if (IsAtStart)
        {
            return false;
        }

        Position--;
        return true;
    }

    /// <summary>Moves back to before the first step.</summary>
    public void Reset() => Position = -1;

    public override string ToString() =>
        Current is { } step ? $"{Position + 1}/{Count}: {step}" : $"before {Count} step(s)";
}
