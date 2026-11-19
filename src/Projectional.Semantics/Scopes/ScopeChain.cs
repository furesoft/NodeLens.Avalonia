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

    private ScopeChain(IScope[] levels) => _levels = levels;

    /// <summary>A chain of <paramref name="levels"/>, nearest first.</summary>
    /// <remarks>
    /// A level that answers eagerly and holds nothing is dropped, because it is known to contribute nothing and a
    /// chain of one level is that level. A level that answers from an index stays even when it happens to be empty:
    /// asking it costs a lookup, while reading its entries is the one thing a chain must not do while it is being
    /// built - that is the whole reason the level is lazy.
    /// </remarks>
    public static IScope Of(params ReadOnlySpan<IScope> levels)
    {
        var kept = new List<IScope>(levels.Length);

        foreach (var level in levels)
        {
            ArgumentNullException.ThrowIfNull(level, nameof(levels));

            if (level is Scope { Entries.Count: 0 })
            {
                continue;
            }

            kept.Add(level);
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
    /// <remarks>
    /// Concatenated on first access. A caller that only resolves a name never asks for it, and asking every level
    /// for its entries at construction is what made a scope cost the document it described.
    /// </remarks>
    public IReadOnlyList<ScopeEntry> Entries => field ??= [.. _levels.SelectMany(level => level.Entries)];

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

    public override string ToString() => $"{_levels.Length} level(s)";
}
