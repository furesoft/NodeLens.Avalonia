using Projectional.Core;

namespace Projectional.Language;

/// <summary>A node that could not produce a value, with the node it happened at.</summary>
/// <remarks>
/// <para>
/// It is an exception rather than a result because a rule reads as an expression - <c>node.Eval() + 1</c> -
/// and a result type would make every rule a chain of matches. The exception never escapes the interpreter:
/// the runtime catches it, records it, and answers an
/// <c>InterpretationResult</c> whose failure names this node and this message.
/// </para>
/// <para>
/// The node travels as a reference because the failure is reported immediately, while the model that produced
/// it is still open. The interpreter turns it into a <c>NodeId</c> when it stores the failure.
/// </para>
/// </remarks>
public sealed class EvaluationException : Exception
{
    /// <summary>Creates the failure a rule reports.</summary>
    /// <param name="node">The node that could not be evaluated.</param>
    /// <param name="message">What is wrong, in the words the user sees.</param>
    public EvaluationException(Node node, string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(node);

        Node = node;
    }

    /// <summary>The node that could not be evaluated.</summary>
    public Node Node { get; }
}
