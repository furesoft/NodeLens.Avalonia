using Projectional.Editing;

namespace Projectional.Core.Tests;

/// <summary>A provider that answers a fixed list, so a test can say what composition and order do.</summary>
/// <remarks>
/// It also records the call count and the context, because two of the rules worth pinning are about the list
/// rather than about any one intention: that providers are asked afresh on every derivation, and that they all
/// see one resolved caret.
/// </remarks>
internal sealed class StubIntentions : IIntentionProvider
{
    private readonly IReadOnlyList<Intention> _intentions;

    internal StubIntentions(params Intention[] intentions) => _intentions = intentions;

    /// <summary>How many times this provider has been asked, which is what "asked afresh" means.</summary>
    internal int Calls { get; private set; }

    /// <summary>The context of the most recent call, or <see langword="null"/> before the first.</summary>
    internal IntentionContext? LastContext { get; private set; }

    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        Calls++;
        LastContext = context;

        return _intentions;
    }
}
