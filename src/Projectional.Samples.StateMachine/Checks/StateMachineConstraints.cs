using Projectional.Core;
using Projectional.Samples.StateMachine;
using Projectional.Semantics;

namespace Projectional.Samples.StateMachine.Checks;

/// <summary>
/// The rule the state machine states about itself: a state nothing leads to can never be entered.
/// </summary>
/// <remarks>
/// <para>
/// A <b>constraint</b> rather than a check, because there is no repair a language may choose on the user's
/// behalf: the state could be wired up, deleted, or meant to be the initial one, and each of those is a design
/// decision. A check owes a repair, so this is a sentence with a squiggle under it - which is exactly the split
/// <c>docs/architecture/semantics.md</c> draws between the two.
/// </para>
/// <para>
/// It is also the finding the framework's catalogue cannot state, which is why it ships beside the language:
/// standard checks are about shapes any language has, and "no transition starts here" is about this one.
/// </para>
/// </remarks>
public static class StateMachineConstraints
{
    /// <summary>The identifier the unreachable-state rule reports under.</summary>
    public const string UnreachableCode = "sm.state.unreachable";

    /// <summary>Every constraint the state machine declares.</summary>
    public static ConstraintSet All { get; } = ConstraintSet.Empty
        .Add(SmConceptKeys.State, new UnreachableState());

    /// <summary>A state that is neither the initial one nor the target of any transition.</summary>
    private sealed class UnreachableState : IConstraint
    {
        /// <inheritdoc />
        /// <remarks>
        /// A state is only judged inside a machine: a state used on its own - the ordinary state of a document
        /// being written, and the root of a file that holds one definition per document - has no initial state
        /// and no transitions to compare against, so there is nothing to be wrong about yet.
        /// </remarks>
        public void Check(ConstraintContext context)
        {
            var state = context.Node;

            if (state.Parent is not { } machine || machine.Concept.Key != SmConceptKeys.Machine)
            {
                return;
            }

            if (machine.GetReferenceTarget(StateMachineLanguage.InitialSlot) is { } initial && initial.Id == state.Id)
            {
                return;
            }

            foreach (var transition in machine.GetChildren(StateMachineLanguage.TransitionsSlot))
            {
                if (transition.GetReferenceTarget("target") is { } target && target.Id == state.Id)
                {
                    return;
                }
            }

            context.Warning(
                $"nothing leads to \"{state.GetOrNull("name")}\", so the machine can never enter it.",
                code: UnreachableCode);
        }
    }
}
