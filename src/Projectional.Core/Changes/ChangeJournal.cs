namespace Projectional.Core;

/// <summary>
/// The list of steps a transaction has taken so far, each with the way back.
/// </summary>
/// <remarks>
/// Rollback is recorded as a closure per step rather than derived from the public change records.
/// The records are deliberately coarse — one <see cref="NodeRemoved"/> for a whole deleted subtree,
/// for example — which makes them pleasant to subscribe to and useless for restoring the exact
/// previous state. Capturing the undo action at the mutation site is the only place where all the
/// state that has to come back is still at hand.
/// </remarks>
internal sealed class ChangeJournal
{
    private readonly List<JournalEntry> _entries = [];

    public int Count => _entries.Count;

    public bool IsEmpty => _entries.Count == 0;

    /// <summary>Adds a step. <paramref name="change"/> is what subscribers will see.</summary>
    public void Add(IModelChange? change, Action undo, Action? redo = null) =>
        _entries.Add(new JournalEntry(change, undo, redo));

    /// <summary>Everything recorded since <paramref name="mark"/>, in order.</summary>
    public IReadOnlyList<IModelChange> ChangesFrom(int mark)
    {
        var changes = new List<IModelChange>(_entries.Count - mark);
        for (var i = mark; i < _entries.Count; i++)
        {
            if (_entries[i].Change is { } change)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    /// <summary>
    /// Every step since <paramref name="mark"/>, in the order the steps happened.
    /// </summary>
    public IReadOnlyList<JournalEntry> StepsFrom(int mark)
    {
        var steps = new List<JournalEntry>(_entries.Count - mark);
        for (var i = mark; i < _entries.Count; i++)
        {
            steps.Add(_entries[i]);
        }

        return steps;
    }

    /// <summary>Undoes every step back to <paramref name="mark"/> and forgets them.</summary>
    public void RollbackTo(int mark)
    {
        for (var i = _entries.Count - 1; i >= mark; i--)
        {
            _entries[i].Undo();
        }

        _entries.RemoveRange(mark, _entries.Count - mark);
    }

    /// <summary>Forgets everything. Used when a committed set has been published.</summary>
    public void Clear() => _entries.Clear();
}
