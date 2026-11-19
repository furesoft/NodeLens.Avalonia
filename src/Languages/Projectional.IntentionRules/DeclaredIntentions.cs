using Projectional.Core;
using Projectional.Editing;

namespace Projectional.IntentionRules;

/// <summary>The intentions one language declared, offered per caret.</summary>
/// <remarks>
/// The entries are built once, when the language loads, and filtered per caret: an intention is declared, not
/// created, and a list that rebuilt them would give a host objects it could not compare or cache. A rule that names
/// no concept is offered on every node, which is how a language states an entry about its vocabulary as a whole.
/// </remarks>
internal sealed class DeclaredIntentions : IIntentionProvider
{
    private readonly IReadOnlyList<DeclaredIntention> _entries;

    internal DeclaredIntentions(IReadOnlyList<DeclaredIntention> entries) => _entries = entries;

    /// <summary>How many intentions the language declared.</summary>
    internal int Count => _entries.Count;

    /// <inheritdoc />
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            .. _entries
                .Where(entry => entry.Applies(context))
                .Select(entry => entry.Intention.With(IntentionAvailability.Applies)),
        ];
    }
}
