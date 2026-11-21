using Projectional.Editing;

namespace Projectional.Languages;

/// <summary>Several intention sources asked at one caret, their answers listed in the order they were given.</summary>
/// <remarks>
/// What an extending language needs: the language's own intentions and the ones it inherited are one catalogue at
/// the caret, and the order is what a reader sees - so the project puts its own first and its bases nearest first.
/// Nothing is de-duplicated: two sources that offer an intention with the same label offer two intentions, and
/// hiding one of them would be a guess about which the user meant.
/// </remarks>
internal sealed class ChainedIntentions : IIntentionProvider
{
    private readonly IReadOnlyList<IIntentionProvider> _providers;

    private ChainedIntentions(IReadOnlyList<IIntentionProvider> providers) => _providers = providers;

    /// <summary>The chain of <paramref name="providers"/>, or the one source itself when there is only one.</summary>
    /// <param name="providers">The sources to ask, in the order their answers are listed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="providers"/> is empty.</exception>
    internal static IIntentionProvider Of(IEnumerable<IIntentionProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        List<IIntentionProvider> sources = [.. providers];

        return sources.Count switch
        {
            0 => throw new ArgumentException("A chain of intention providers needs at least one source.", nameof(providers)),
            1 => sources[0],
            _ => new ChainedIntentions(sources),
        };
    }

    /// <summary>Every source's intentions for this caret, source by source.</summary>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<Intention> intentions = [];

        foreach (var provider in _providers)
        {
            intentions.AddRange(provider.At(context));
        }

        return intentions;
    }

    public override string ToString() => $"{_providers.Count} intention source(s)";
}
