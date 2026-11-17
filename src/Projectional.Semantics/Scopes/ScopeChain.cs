namespace Projectional.Semantics;

/// <summary>
/// Several scopes in priority order: the first level that knows a name decides what it means.
/// </summary>
/// <remarks>
/// <para>
/// This is the "first match wins" rule of the scope model, and it is the whole reason a chain exists:
/// a name declared next to the reference shadows one declared in the enclosing block, which shadows one
/// declared at the top of the model. A level that knows the name but knows it twice makes the name
/// ambiguous - the chain does not fall through to a lower level looking for a unique answer, because
/// that would make a nearer declaration lose to a farther one.
/// </para>
/// <para>
/// <see cref="Entries"/> concatenates every level, nearest first, which is the order a completion list
/// should show them in. It does not deduplicate: the same name at two levels is two entries, and the
/// list is where the user sees that the nearer one wins.
/// </para>
/// </remarks>
public sealed class ScopeChain : IScope
{
    private readonly IScope[] _levels;
    private readonly ScopeEntry[] _entries;

    private ScopeChain(IScope[] levels)
    {
        _levels = levels;
        _entries = [.. levels.SelectMany(level => level.Entries)];
    }

    /// <summary>A chain of <paramref name="levels"/>, nearest first.</summary>
    public static IScope Of(params ReadOnlySpan<IScope> levels)
    {
        var kept = new List<IScope>(levels.Length);

        foreach (var level in levels)
        {
            ArgumentNullException.ThrowIfNull(level, nameof(levels));

            if (level.Entries.Count > 0)
            {
                kept.Add(level);
            }
        }

        return kept.Count switch
        {
            0 => Scope.Empty,
            1 => kept[0],
            _ => new ScopeChain([.. kept]),
        };
    }

    /// <summary>The levels, nearest first.</summary>
    public IReadOnlyList<IScope> Levels => _levels;

    /// <inheritdoc />
    public IReadOnlyList<ScopeEntry> Entries => _entries;

    /// <inheritdoc />
    public ScopeResolution Resolve(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (var level in _levels)
        {
            var resolution = level.Resolve(name);

            if (resolution.Kind != ScopeResolutionKind.Missing)
            {
                return resolution;
            }
        }

        return ScopeResolution.Missing;
    }

    public override string ToString() => $"{_levels.Length} level(s), {_entries.Length} entr(y/ies)";
}
