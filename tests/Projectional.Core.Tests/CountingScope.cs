using Projectional.Semantics;

namespace Projectional.Core.Tests;

/// <summary>A scope level that counts how often a caller reads its entries.</summary>
/// <remarks>
/// Laziness is a cost decision, so the only way to prove it is to ask the level. A chain that concatenated every
/// level's entries while it was being built would still answer correctly - which is exactly why the behaviour needs a
/// counter rather than an assertion about results.
/// </remarks>
internal sealed class CountingScope : IScope
{
    private readonly ScopeEntry[] _entries;

    public CountingScope(params ScopeEntry[] entries) => _entries = entries;

    /// <summary>How often <see cref="Entries"/> was read.</summary>
    public int Reads { get; private set; }

    public IReadOnlyList<ScopeEntry> Entries
    {
        get
        {
            Reads++;

            return _entries;
        }
    }

    public ScopeResolution Resolve(string name)
    {
        var matches = _entries.Where(entry => string.Equals(entry.Name, name, StringComparison.Ordinal)).ToArray();

        return matches switch
        {
            [] => ScopeResolution.Missing,
            [var only] => ScopeResolution.Found(only),
            _ => ScopeResolution.Ambiguous(matches),
        };
    }
}
