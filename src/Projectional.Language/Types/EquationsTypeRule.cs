namespace Projectional.Language;

/// <summary>A body that states relations, which the solver satisfies instead of the rule answering a type.</summary>
/// <param name="equations">The relations the node states about the types around it.</param>
/// <remarks>
/// <see cref="TypeFor"/> answers <see cref="UnknownType"/> because a rule that states relations has not said what type
/// the node is - it has said what must hold for the types around it. Answering anything else would be the guess the
/// solver exists to avoid, and the solver reads the equations from <see cref="ITypeRule.EquationsFor"/> precisely so
/// the two answers stay separate.
/// </remarks>
public sealed class EquationsTypeRule(IReadOnlyList<TypeEquation> equations) : ITypeRule
{
    /// <summary>The relations the node states about the types around it.</summary>
    public IReadOnlyList<TypeEquation> Equations { get; } = equations;

    /// <inheritdoc />
    public IType TypeFor(TypeContext context) => UnknownType.Instance;

    /// <inheritdoc />
    public IEnumerable<TypeEquation> EquationsFor(TypeContext context) => Equations;
}