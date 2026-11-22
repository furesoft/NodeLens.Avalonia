namespace Projectional.Language;

/// <summary>The common supertype of the types of two of the node's children.</summary>
/// <param name="left">The first child slot.</param>
/// <param name="right">The second child slot.</param>
/// <remarks>
/// <para>
/// The one answer a rule could not give before: <see cref="ChildTypeRule"/> names one child and a
/// <see cref="JoinType"/> joins two <em>written</em> types, so "the widest of these two children" had no spelling. A
/// conditional needs exactly that - <c>if c then 1 else "x"</c> is neither branch's type.
/// </para>
/// <para>
/// A child that is not filled contributes nothing rather than its <see cref="UnknownType"/>: a half-written
/// conditional is the ordinary state of a document being edited, and joining against an unknown would answer unknown
/// and lose the type the written branch just earned. With neither filled the answer is unknown, because nothing said
/// anything.
/// </para>
/// </remarks>
public sealed class WidestTypeRule(string left, string right) : ITypeRule
{
    /// <summary>The first child slot.</summary>
    public string Left { get; } = left;

    /// <summary>The second child slot.</summary>
    public string Right { get; } = right;

    /// <inheritdoc />
    public IType TypeFor(TypeContext context)
    {
        if (context.Node.GetChild(Left) is null)
        {
            return context.Node.GetChild(Right) is null
                ? UnknownType.Instance
                : context.TypeOfChild(Right);
        }

        if (context.Node.GetChild(Right) is null)
        {
            return context.TypeOfChild(Left);
        }

        return TypeLattice.Join(context.Subtypes, context.TypeOfChild(Left), context.TypeOfChild(Right));
    }
}