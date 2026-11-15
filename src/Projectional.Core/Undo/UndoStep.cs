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
/// <param name="revision">The history state this step brought about.</param>
/// <remarks>
/// A step owns its closures for as long as it lives, which is why undoing and redoing never has to
/// re-derive them from change records — the redo of a move is the very same re-link the user
/// performed, not a reconstruction from "removed here, added there".
/// </remarks>
internal sealed class UndoStep(IEnumerable<JournalEntry> entries, string? coalescingKey, long revision)
{
    private readonly List<JournalEntry> _entries = [.. entries];

    /// <summary>The history state this step brought about.</summary>
    /// <remarks>
    /// Equal numbers mean the same state and different numbers mean different states, which is what a
    /// host needs to answer "is this document modified". A later transaction merging into the step
    /// moves the number on, so saving in the middle of a typing burst still leaves the document
    /// modified: the burst goes on to change content that was not in the file.
    /// </remarks>
    internal long Revision { get; private set; } = revision;

    /// <summary>
    /// The key the transaction was committed under, or <see langword="null"/> when it refuses to be
    /// merged with anything.
    /// </summary>
    internal string? CoalescingKey { get; } = coalescingKey;

    internal IReadOnlyList<JournalEntry> Entries => _entries;

    /// <summary>Folds a later transaction into this one, so that both undo together.</summary>
    /// <param name="later">The entries of the joining transaction.</param>
    /// <param name="revision">The history state the joined step now stands for.</param>
    internal void Merge(IEnumerable<JournalEntry> later, long revision)
    {
        _entries.AddRange(later);
        Revision = revision;
    }
}
