using System.Collections.ObjectModel;

namespace Projectional.Editing;

/// <summary>The offers at one caret, waiting for the user to pick one.</summary>
/// <remarks>
/// <para>
/// The list a gesture opened, and view state like the completion list: it is derived from the model and the
/// caret, never persisted and never an undo step. It exists only because a list of several offers needs
/// somewhere to live between the keystroke that asked and the click or the arrow key that answers.
/// </para>
/// <para>
/// It holds <see cref="Intention"/> entries rather than repairs, because the user sees one list and the two
/// sources agree on that vocabulary: a quick fix is an intention carrying its <see cref="Intention.Fix"/>, so
/// accepting one runs the repair through the fix's own path. That is why there is no second list type for the
/// second source.
/// </para>
/// <para>
/// It is never empty: none is the absence of an answer rather than an empty list. One entry is ordinary - the
/// gesture always shows the list, so a caret with a single offer gets a single row and one `Enter`. Making the
/// gesture run that row by itself would give one keystroke two meanings, "do it" and "show me what there is", and
/// the user cannot know which one is about to happen.
/// </para>
/// </remarks>
public sealed class OfferList
{
    private readonly ReadOnlyCollection<Intention> _entries;

    /// <summary>Offers <paramref name="entries"/>, which must hold at least one offer.</summary>
    /// <param name="entries">The offers, in the order they should be listed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No offers were given, or one of them says it is absent.</exception>
    public OfferList(IReadOnlyList<Intention> entries)
        : this(Validated(entries), 0)
    {
    }

    private OfferList(ReadOnlyCollection<Intention> entries, int selected)
    {
        _entries = entries;
        Selected = selected;
    }

    /// <summary>The offers, in the order they are listed.</summary>
    public IReadOnlyList<Intention> Entries => _entries;

    /// <summary>Which offer is highlighted, counted from zero.</summary>
    /// <remarks>
    /// The list opens on the first entry, which is the host's first applicable offer or the worst finding's
    /// repair - so the entry the user most likely wants is the one already under the highlight.
    /// </remarks>
    public int Selected { get; }

    /// <summary>The highlighted offer.</summary>
    public Intention SelectedEntry => _entries[Selected];

    /// <summary>The same offers with <paramref name="index"/> highlighted, clamped into the list.</summary>
    /// <param name="index">Which offer to highlight.</param>
    /// <remarks>
    /// A new list rather than a mutation, so the one a caller is holding cannot change underneath it.
    /// The index is clamped rather than wrapped: a list of offers is not a carousel, and pressing Down
    /// once too often should stop at the end where the user can see it stopped.
    /// </remarks>
    public OfferList With(int index) => new(_entries, Math.Clamp(index, 0, _entries.Count - 1));

    /// <summary>The same offers with the highlight moved by <paramref name="delta"/> rows.</summary>
    /// <param name="delta">How many rows to move, negative towards the front.</param>
    public OfferList Move(int delta) => With(Selected + delta);

    /// <summary>How many offers are listed.</summary>
    public int Count => _entries.Count;

    /// <summary>The offer at <paramref name="index"/>.</summary>
    /// <param name="index">Which offer, counted from zero in the order listed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the list.</exception>
    public Intention this[int index] => _entries[index];

    public override string ToString() => $"{_entries.Count} offers";

    /// <summary>Copies and checks the offers for the real constructor to index into.</summary>
    private static ReadOnlyCollection<Intention> Validated(IReadOnlyList<Intention> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new ArgumentException(
                "An offer list holds at least one entry; no offers at all is no answer rather than a list.",
                nameof(entries));
        }

        var copy = new List<Intention>(entries.Count);

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entries));

            // The list is what the user chooses from, so an entry that said "not this caret" would be a row
            // that cannot be chosen for a reason the user never asked about.
            if (entry.IsAbsent)
            {
                throw new ArgumentException(
                    $"The offer \"{entry.Id}\" is absent at this caret, so it cannot be listed.",
                    nameof(entries));
            }

            copy.Add(entry);
        }

        return copy.AsReadOnly();
    }
}
