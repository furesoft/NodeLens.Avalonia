namespace Projectional.Core;

/// <summary>
/// The undo history of one model: a stack of committed transactions that can be taken back and put
/// forward again.
/// </summary>
/// <remarks>
/// <para>
/// The stack listens to <see cref="Model.Changed"/> rather than wrapping the edit API, so nothing in
/// the model has to know that undo exists and every path that journals an edit is undoable for free:
/// a keystroke, a generated block of a hundred nodes, a drag that moved a subtree, a transaction the
/// host application defined itself.
/// </para>
/// <para>
/// Undo and redo run the recorded closures inside a transaction of their own and then publish the
/// <em>inverted</em> change records, so a subscriber learns about a restoration exactly as it learns
/// about an edit. A redo publishes the original records again, which is what makes a projection
/// invalidate the same views twice.
/// </para>
/// </remarks>
public sealed class UndoHistory : IDisposable
{
    private readonly Model _model;
    private readonly List<UndoStep> _undo = [];
    private readonly List<UndoStep> _redo = [];
    private bool _replaying;
    private bool _disposed;

    public UndoHistory(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;
        _model.Changed += OnModelChanged;
    }

    /// <summary>Whether there is a committed transaction that can be taken back.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether there is a taken-back transaction that can be put forward again.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>How many undo presses are left.</summary>
    public int UndoDepth => _undo.Count;

    /// <summary>How many redo presses are left.</summary>
    public int RedoDepth => _redo.Count;

    /// <summary>
    /// The key the next committed transaction is grouped under, or <see langword="null"/> to keep it
    /// a step of its own.
    /// </summary>
    /// <remarks>
    /// Consecutive commits that carry the same non-null key collapse into one undo step, which is how
    /// typing into a slot becomes one undo instead of one per keystroke. The key is not a time
    /// window, so a caller driving the coalescing has to clear it when the burst of edits ends —
    /// otherwise the next transaction that forgets to set a key of its own would join the burst.
    /// </remarks>
    public string? CoalescingKey { get; set; }

    /// <summary>Takes the last committed transaction back.</summary>
    /// <returns>Whether there was anything to take back.</returns>
    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Replay(step, backwards: true);
        _redo.Add(step);
        return true;
    }

    /// <summary>Puts the last taken-back transaction forward again.</summary>
    /// <returns>Whether there was anything to redo.</returns>
    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Replay(step, backwards: false);
        _undo.Add(step);
        return true;
    }

    /// <summary>Forgets the whole history, as a fresh load of a model needs.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        CoalescingKey = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _model.Changed -= OnModelChanged;
    }

    private void OnModelChanged(object? sender, ModelChangedEventArgs e)
    {
        if (_replaying || e.Steps.Count == 0)
        {
            return;
        }

        _redo.Clear();

        if (CoalescingKey is { } key && _undo.Count > 0 && _undo[^1].CoalescingKey == key)
        {
            _undo[^1].Merge(e.Steps);
            return;
        }

        _undo.Add(new UndoStep(e.Steps, CoalescingKey));
    }

    private void Replay(UndoStep step, bool backwards)
    {
        _replaying = true;
        try
        {
            using var transaction = _model.BeginTransaction();

            if (backwards)
            {
                for (var i = step.Entries.Count - 1; i >= 0; i--)
                {
                    step.Entries[i].Undo();
                }
            }
            else
            {
                foreach (var entry in step.Entries)
                {
                    entry.Redo?.Invoke();
                }
            }

            _model.MarkMutated();
            transaction.Complete();

            _model.PublishRestored(Describe(step, backwards));
        }
        finally
        {
            _replaying = false;
        }
    }

    /// <summary>
    /// The change set a replay is worth: the inverse records when going back, and the original ones
    /// when going forward.
    /// </summary>
    private static IModelChange[] Describe(UndoStep step, bool backwards)
    {
        var described = new List<IModelChange>(step.Entries.Count);

        if (backwards)
        {
            for (var i = step.Entries.Count - 1; i >= 0; i--)
            {
                if (step.Entries[i].Change is { } change)
                {
                    described.Add(change.Invert());
                }
            }

            return [.. described];
        }

        foreach (var entry in step.Entries)
        {
            if (entry.Change is { } change)
            {
                described.Add(change);
            }
        }

        return [.. described];
    }
}
