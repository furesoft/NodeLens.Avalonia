using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>Why a generator produced no text, and where it stopped.</summary>
/// <param name="Message">What is wrong, in the words the user sees.</param>
/// <param name="Node">The node whose template could not be applied.</param>
/// <remarks>
/// The same bargain an interpretation failure makes: a missing template, an empty slot a template needed and a
/// template that expands itself are all things a user has to be told, and none of them should take the host down. It
/// carries a node id because the result outlives the call that produced it.
/// </remarks>
public sealed record GenerationFailure(string Message, NodeId Node)
{
    public override string ToString() => Message;
}
