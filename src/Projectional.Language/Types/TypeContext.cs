using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// What one type rule sees: the node, and the way to ask what its children are.
/// </summary>
/// <remarks>
/// <para>
/// The context is small on purpose, like a constraint's: it can read the node and ask for a child's type,
/// and nothing else. A rule that could mutate, or reach the editor, would be a second editing path - and a
/// rule that ran on a model nobody is drawing would stop working.
/// </para>
/// <para>
/// <see cref="TypeOf(Node)"/> is the local propagation the design allows. It is not inference: it asks
/// the same registry the same question about a different node, so a cycle in the model would be a cycle
/// here. The model refuses cycles, so this does not guard against one.
/// </para>
/// </remarks>
public sealed class TypeContext
{
    private readonly TypeRegistry _registry;

    internal TypeContext(Node node, TypeRegistry registry)
    {
        Node = node;
        _registry = registry;
    }

    /// <summary>The node whose type is being computed.</summary>
    public Node Node { get; }

    /// <summary>What <paramref name="node"/> evaluates to.</summary>
    public IType TypeOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return _registry.TypeOf(node);
    }

    /// <summary>
    /// What the single child in <paramref name="slot"/> evaluates to, or <see cref="UnknownType"/> when the
    /// slot holds nothing.
    /// </summary>
    /// <remarks>
    /// An empty slot is unknown rather than an error: a half-written expression is the ordinary state of a
    /// document being edited, and a rule that threw on it would make the editor unusable while typing.
    /// </remarks>
    public IType TypeOfChild(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        var children = Node.GetChildren(slot);

        return children.Count > 0 ? _registry.TypeOf(children[0]) : UnknownType.Instance;
    }
}
