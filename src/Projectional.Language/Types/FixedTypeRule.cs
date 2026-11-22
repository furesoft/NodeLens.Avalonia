namespace Projectional.Language;

/// <summary>A type written out, which is the same whatever the node holds.</summary>
/// <param name="type">The type every node of the concept evaluates to.</param>
/// <remarks>
/// One of the five shapes a declared type rule lowers to, and public because a language may also be written in C#:
/// an author who wants "every literal of this concept is an <c>int</c>" should not have to write a class for it, and an
/// export has to be able to say the same thing in the code it generates.
/// </remarks>
public sealed class FixedTypeRule(IType type) : ITypeRule
{
    /// <summary>The type every node of the concept evaluates to.</summary>
    public IType Type { get; } = type;

    /// <inheritdoc />
    public IType TypeFor(TypeContext context) => Type;
}