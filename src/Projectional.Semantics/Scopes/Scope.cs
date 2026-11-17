namespace Projectional.Semantics;

/// <summary>
/// One level of a scope chain: the entries a single rule contributes, in the order it contributes them.
/// </summary>
/// <remarks>
/// <para>
/// A level is deliberately not deduplicated. Two entries with one name inside a level are what makes a
/// name ambiguous, and collapsing them here would turn a decision the user has to make into a silent
/// pick. Across levels the first level that knows the name wins - that is
/// <see cref="ScopeChain"/>'s job, not this one's.
/// </para>
/// <para>
/// The value is immutable, so a caller can hold one while the document changes underneath.
/// </para>
/// </remarks>
public sealed class Scope : IScope
{
    private readonly ScopeEntry[] _entries;

    private Scope(ScopeEntry[] entries) => _entries = entries;

    /// <summary>A level that contributes nothing.</summary>
    public static Scope Empty { get; } = new([]);

    /// <inheritdoc />
    public IReadOnlyList<ScopeEntry> Entries => _entries;

    /// <summary>A level holding <paramref name="entries"/>, in the order given.</summary>
    public static Scope Of(IEnumerable<ScopeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var built = entries.ToArray();

        return built.Length == 0 ? Empty : new Scope(built);
    }

    /// <summary>A level holding one entry.</summary>
    public static Scope Of(ScopeEntry entry) => new([entry]);

    /// <inheritdoc />
    public ScopeResolution Resolve(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        List<ScopeEntry>? matches = null;

        foreach (var entry in _entries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            matches ??= [];
            matches.Add(entry);
        }

        return matches switch
        {
            null => ScopeResolution.Missing,
            [var only] => ScopeResolution.Found(only),
            _ => ScopeResolution.Ambiguous(matches),
        };
    }

    public override string ToString() => $"{_entries.Length} entr(y/ies)";
}
