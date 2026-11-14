namespace Projectional.Core;

/// <summary>
/// Everything one undo press takes back: the steps of a single committed transaction, together with
/// the key that decides whether a later transaction may still join it.
/// </summary>
/// <param name="entries">The steps of the transaction, in the order they happened.</param>
/// <param name="coalescingKey">
/// The key the transaction was committed under, or <see langword="null"/> when it refuses to be
/// merged with anything.
/// </param>
/// <remarks>
/// A step owns its closures for as long as it lives, which is why undoing and redoing never has to
/// re-derive them from change records — the redo of a move is the very same re-link the user
/// performed, not a reconstruction from "removed here, added there".
/// </remarks>
internal sealed class UndoStep(IEnumerable<JournalEntry> entries, string? coalescingKey)
{
    private readonly List<JournalEntry> _entries = [.. entries];

    /// <summary>
    /// The key the transaction was committed under, or <see langword="null"/> when it refuses to be
    /// merged with anything.
    /// </summary>
    internal string? CoalescingKey { get; } = coalescingKey;

    internal IReadOnlyList<JournalEntry> Entries => _entries;

    /// <summary>Folds a later transaction into this one, so that both undo together.</summary>
    internal void Merge(IEnumerable<JournalEntry> later) => _entries.AddRange(later);
}
