using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>Why an interpretation produced no value, and where it stopped.</summary>
/// <param name="Kind">Which of the three ways the run ended.</param>
/// <param name="Message">What is wrong, in the words the user sees.</param>
/// <param name="Node">The node the failure happened at, or <see langword="null"/> when there is none.</param>
/// <remarks>
/// A failure is an answer, not an exception: a decision table that matches no rule, a name the run does not
/// bind and a model that runs too long are all things a user has to be told, and none of them should take the
/// host down. It carries a node id rather than a node because the result outlives the call that produced it -
/// and because a host navigates to <c>NodeId</c> anyway.
/// </remarks>
public sealed record InterpretationFailure(InterpretationFailureKind Kind, string Message, NodeId? Node)
{
    public override string ToString() => $"{Kind}: {Message}";
}
