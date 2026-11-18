using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;

namespace Projectional.Samples.StateMachine;

/// <summary>What a state and a transition mean: a predicate per arrow, and a walk per machine.</summary>
/// <remarks>
/// <para>
/// The same split the pilot's decision list makes, and for the same reason: a <b>transition</b> is a predicate -
/// does its guard hold - so it can be read on its own, while a <b>machine</b> is the walk that decides where the
/// machine ends up. Both are in the trace, so "why did it stop here" is answered by the lines under the machine.
/// </para>
/// <para>
/// A state has no evaluation rule on purpose. Asking what a state *is* would have to answer a value nobody asked
/// for, and the interpreter reports a missing rule rather than inventing one.
/// </para>
/// </remarks>
public static class StateMachineEvaluation
{
    /// <summary>How many transitions one run may take before it is called a loop.</summary>
    /// <remarks>
    /// A machine can cycle - `Idle -> Running -> Idle` is an ordinary design - so a walk has to be bounded
    /// somewhere. The bound is a fact about a run, not about the document: the interpreter's own step budget
    /// stops a runaway evaluation, and this stops a machine that never reaches a final state.
    /// </remarks>
    public const int MaxSteps = 100;

    /// <summary>The rules of <see cref="StateMachineLanguage"/>, composed on top of the expression rules.</summary>
    public static EvaluationRegistry Registry { get; } = ExpressionsEvaluation.Registry
        .Add(SmConceptKeys.Transition, new DelegateEvaluationRule(GuardHolds))
        .Add(SmConceptKeys.Machine, new DelegateEvaluationRule(Walk));

    /// <summary>A transition applies when it has no guard, or when its guard is true.</summary>
    private static BoolValue GuardHolds(EvaluationContext context) =>
        context.Node.GetChild("guard") is null ? new BoolValue(true) : context.EvalChild("guard") is BoolValue guard
            ? guard
            : throw context.Error("A transition's guard must be a truth value.");

    /// <summary>Walks from the initial state, taking the first applicable transition each time.</summary>
    private static StringValue Walk(EvaluationContext context)
    {
        var machine = context.Node;

        if (machine.GetReferenceTarget(StateMachineLanguage.InitialSlot) is not { } current)
        {
            throw context.Error("The machine names no initial state, so there is nowhere to start.");
        }

        List<string> path = [NameOf(current)];

        for (var step = 0; step < MaxSteps; step++)
        {
            if (IsFinal(current))
            {
                return new StringValue(string.Join(" -> ", path));
            }

            var next = Next(context, machine, current);

            if (next is null)
            {
                throw context.Error(
                    $"The machine stops in \"{NameOf(current)}\", which is neither final nor has a transition that applies.");
            }

            current = next;
            path.Add(NameOf(current));
        }

        throw context.Error(
            $"The machine took {MaxSteps} transitions without reaching a final state, so it does not settle.");
    }

    /// <summary>The target of the first transition that starts here and applies, or <see langword="null"/>.</summary>
    /// <remarks>
    /// "First" is document order, which is the order the reader sees - the same rule the pilot's rule table
    /// follows. A transition whose source is unset belongs to no state and is skipped rather than reported: an
    /// unfinished arrow is the ordinary state of a document being written.
    /// </remarks>
    private static Node? Next(EvaluationContext context, Node machine, Node current)
    {
        foreach (var transition in machine.GetChildren(StateMachineLanguage.TransitionsSlot))
        {
            if (transition.GetReferenceTarget("source") is not { } source
                || source.Id != current.Id
                || transition.GetReferenceTarget("target") is not { } target)
            {
                continue;
            }

            if (context.Eval(transition) is BoolValue { Content: true })
            {
                return target;
            }
        }

        return null;
    }

    private static bool IsFinal(Node state) => state.GetOrNull("isFinal") == "true";

    private static string NameOf(Node state) => state.GetOrNull("name") is { Length: > 0 } name ? name : "<unnamed>";
}
