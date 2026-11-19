using System.Collections.ObjectModel;

using Projectional.Core;

namespace Projectional.Semantics;

/// <summary>
/// A provider a language declares: for the references it names, one document place, and the default chain for
/// everything else.
/// </summary>
/// <remarks>
/// <para>
/// It sits beside <see cref="DefaultScopeProvider"/> rather than replacing it. A concept may own several references,
/// and a language usually says something about one of them - "a rule's condition sees the rules of its ruleset" says
/// nothing about the rule's other references - so an unnamed slot falls back to the default chain instead of being
/// answered by a level the language never asked for. That fallback is also what makes a declared provider safe to
/// install for a whole concept.
/// </para>
/// <para>
/// The bindings are keyed by slot name, because a scope is a fact about the model and the slot is its address: the
/// same reference has the same scope whether it is drawn in a line, in a table or not at all.
/// </para>
/// </remarks>
public sealed class NamedScopeProvider : IScopeProvider
{
    private readonly ReadOnlyDictionary<string, ScopeBinding> _slots;

    /// <summary>A provider that answers the named slots and hands every other slot to the default chain.</summary>
    /// <param name="slots">The slots it answers for, each with where its names come from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="slots"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A slot name is blank.</exception>
    public NamedScopeProvider(IReadOnlyDictionary<string, ScopeBinding> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var copy = new Dictionary<string, ScopeBinding>(StringComparer.Ordinal);

        foreach (var (slot, binding) in slots)
        {
            ArgumentException.ThrowIfNullOrEmpty(slot);

            copy[slot] = binding;
        }

        _slots = new ReadOnlyDictionary<string, ScopeBinding>(copy);
    }

    /// <summary>The slots this provider answers for, and where each one's names come from.</summary>
    public IReadOnlyDictionary<string, ScopeBinding> Slots => _slots;

    /// <summary>A provider answering one slot, which is the ordinary case for a language written in C#.</summary>
    /// <param name="slot">The reference slot it answers for.</param>
    /// <param name="place">Where that slot's names are written.</param>
    /// <param name="target">The concept the visible nodes must be, or <see langword="null"/> for any named node.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is blank.</exception>
    public static NamedScopeProvider For(string slot, ScopePlace place, string? target = null) =>
        new(new Dictionary<string, ScopeBinding>(StringComparer.Ordinal) { [slot] = new(place, target) });

    /// <inheritdoc />
    public IScope ScopeOf(Node node, string referenceSlot)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(referenceSlot);

        return _slots.TryGetValue(referenceSlot, out var binding)
            ? ScopeChain.Of([.. ScopeWalk.Levels(node, binding.Place, binding.Target)])
            : DefaultScopeProvider.Instance.ScopeOf(node, referenceSlot);
    }

    public override string ToString() =>
        string.Join(", ", _slots.Select(slot => $"{slot.Key}={slot.Value}"));
}
