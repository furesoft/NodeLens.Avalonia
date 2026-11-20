using Projectional.Core;

namespace Projectional.Language;

/// <summary>A pure function that computes what a node is called, written as data.</summary>
/// <remarks>
/// <para>
/// The sibling of a template: a closed set of parts that reads slots and chooses, never computes. It is what a
/// declaration passes as <c>ConceptDeclaration.NameOf</c>, so the three shapes a name slot cannot express - a name
/// that is a value, a name spread over a child, and a name that is a sentence - are declared rather than coded
/// (docs/architecture/behaviours.md).
/// </para>
/// <para>
/// <b>Total.</b> Every part answers a string, and an empty slot answers the empty string, so a half-written document -
/// the state a document is in most of the time - never makes a reference cell's build fail. <b>Bounded.</b> A part may
/// name another node after its child or after its reference target, so a cycle is writable; the evaluation carries a
/// depth budget and answers nothing beyond it, because a <see cref="StackOverflowException"/> cannot be reported to a
/// user. The budget is a thread-static counter rather than an argument, because the recursion passes through
/// <see cref="Node.Name"/> of the other concept, where no argument is carried.
/// </para>
/// </remarks>
public abstract record NameBody
{
    /// <summary>How deep an evaluation may nest before it answers nothing at all.</summary>
    /// <remarks>
    /// A guard, not a budget: the depth a real name reaches is bounded by its document. A cycle that comes back through
    /// a reference is the case this exists for.
    /// </remarks>
    public const int MaxDepth = 32;

    [ThreadStatic]
    private static int _depth;

    /// <summary>The function a declaration takes.</summary>
    /// <returns>A function that answers what the node is called, never <see langword="null"/>.</returns>
    public Func<Node, string?> ToFunction() => NameOf;

    /// <summary>What <paramref name="node"/> is called by this body.</summary>
    /// <param name="node">The node to name.</param>
    /// <returns>The name, empty when the body answers nothing or the depth budget is spent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public string NameOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_depth >= MaxDepth)
        {
            return string.Empty;
        }

        _depth++;

        try
        {
            return Evaluate(node);
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>What this part answers for <paramref name="node"/>; never <see langword="null"/>.</summary>
    private protected abstract string Evaluate(Node node);
}
