using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// The scope providers a language declares, keyed by concept, with the default chain behind them.
/// </summary>
/// <remarks>
/// <para>
/// This is the "a language can replace any step" half of the scope model. A concept that declares a
/// provider answers for itself and for every concept below it - the lookup walks the super concepts in
/// declaration order, the same way notation and triggers resolve - and everything else falls through to
/// <see cref="DefaultScopeProvider"/>.
/// </para>
/// <para>
/// It is itself an <see cref="IScopeProvider"/>, so a host installs one by assigning it to
/// <c>EditorSession.Scopes</c> and nothing else changes. That is deliberate: a second kind of provider
/// would mean a second place that decides which one wins.
/// </para>
/// <para>
/// The registry is immutable and <see cref="Add"/> answers a new one, so a language can be composed
/// without mutating a registry another session is using.
/// </para>
/// </remarks>
public sealed class ScopeProviderRegistry : IScopeProvider
{
    private readonly Dictionary<string, IScopeProvider> _providers;

    private ScopeProviderRegistry(Dictionary<string, IScopeProvider> providers) => _providers = providers;

    /// <summary>A registry that declares nothing, so every concept gets the default chain.</summary>
    public static ScopeProviderRegistry Empty { get; } = new([]);

    /// <summary>How many concepts declare a provider.</summary>
    public int Count => _providers.Count;

    /// <summary>The same registry with <paramref name="conceptKey"/> answered by <paramref name="provider"/>.</summary>
    /// <exception cref="ArgumentException">The concept key is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public ScopeProviderRegistry Add(string conceptKey, IScopeProvider provider)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentNullException.ThrowIfNull(provider);

        var providers = new Dictionary<string, IScopeProvider>(_providers, StringComparer.Ordinal)
        {
            [conceptKey] = provider,
        };

        return new ScopeProviderRegistry(providers);
    }

    /// <summary>
    /// The provider that answers for <paramref name="concept"/>, or <see langword="null"/> when the
    /// default chain does.
    /// </summary>
    /// <remarks>
    /// The concept itself first, then its super concepts in declaration order - the same order
    /// <see cref="ConceptRegistry"/> uses for notation and triggers, so a language has one rule for
    /// "which declaration wins" rather than one per aspect.
    /// </remarks>
    public IScopeProvider? ProviderFor(Concept concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (_providers.Count == 0)
        {
            return null;
        }

        return Find(concept, []);
    }

    /// <inheritdoc />
    public IScope ScopeOf(Node node, string referenceSlot)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(referenceSlot);

        return (ProviderFor(node.Concept) ?? DefaultScopeProvider.Instance).ScopeOf(node, referenceSlot);
    }

    private IScopeProvider? Find(Concept concept, HashSet<string> seen)
    {
        if (!seen.Add(concept.Key))
        {
            return null;
        }

        if (_providers.TryGetValue(concept.Key, out var provider))
        {
            return provider;
        }

        foreach (var super in concept.SuperConcepts)
        {
            if (Find(super, seen) is { } inherited)
            {
                return inherited;
            }
        }

        return null;
    }

    public override string ToString() => $"{_providers.Count} provider(s)";
}
