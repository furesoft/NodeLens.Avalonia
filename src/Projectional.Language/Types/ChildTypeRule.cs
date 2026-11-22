namespace Projectional.Language;

/// <summary>The type of one of the node's children.</summary>
/// <param name="slot">The child slot whose type is this node's type.</param>
public sealed class ChildTypeRule(string slot) : ITypeRule
{
    /// <summary>The child slot whose type is this node's type.</summary>
    public string Slot { get; } = slot;

    /// <inheritdoc />
    public IType TypeFor(TypeContext context) => context.TypeOfChild(Slot);
}