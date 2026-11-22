namespace Projectional.Language;

/// <summary>A choice between bodies, decided by a property's value.</summary>
/// <param name="property">The property whose value selects the case.</param>
/// <param name="cases">One rule per value of that property.</param>
/// <param name="otherwise">The rule for every other value, which is what an unwritten property selects.</param>
public sealed class SwitchTypeRule(
    string property,
    IReadOnlyDictionary<string, ITypeRule> cases,
    ITypeRule otherwise) : ITypeRule
{
    /// <summary>The property whose value selects the case.</summary>
    public string Property { get; } = property;

    /// <summary>One rule per value of that property.</summary>
    public IReadOnlyDictionary<string, ITypeRule> Cases { get; } = cases;

    /// <summary>The rule for every other value, which is what an unwritten property selects.</summary>
    public ITypeRule Otherwise { get; } = otherwise;

    /// <inheritdoc />
    public IType TypeFor(TypeContext context)
    {
        var value = context.Node.GetOrNull(Property);

        return value is not null && Cases.TryGetValue(value, out var rule)
            ? rule.TypeFor(context)
            : Otherwise.TypeFor(context);
    }
}