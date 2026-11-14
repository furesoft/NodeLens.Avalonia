namespace Projectional.Core;

/// <summary>
/// Everything one committed transaction changed, published as a single event.
/// </summary>
/// <remarks>
/// A transaction publishes once, no matter how many single edits it performed, so that a bulk
/// operation — a migration, a generator inserting a hundred nodes — costs subscribers one pass and
/// costs undo one step.
/// </remarks>
public sealed class ModelChangedEventArgs : EventArgs
{
    internal ModelChangedEventArgs(
        ModelId model,
        long version,
        IReadOnlyList<IModelChange> changes,
        IReadOnlyList<JournalEntry> steps)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(steps);

        Model = model;
        Version = version;
        Changes = changes;
        Steps = steps;
    }

    public ModelId Model { get; }

    /// <summary>The model's <see cref="Model.Version"/> after this set was applied.</summary>
    public long Version { get; }

    /// <summary>The changes in the order they happened.</summary>
    public IReadOnlyList<IModelChange> Changes { get; }

    /// <summary>
    /// The steps that produced <see cref="Changes"/>, in the order they happened, each with the way
    /// back and the way forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately internal: undo is the only legitimate consumer. A host application that could
    /// reach these closures could rewrite a model without going through a transaction, losing the
    /// change records that projection, semantics and persistence rely on.
    /// </para>
    /// <para>
    /// Empty when the set comes from an undo or a redo: the history already holds those steps and
    /// does not want them handed back to it.
    /// </para>
    /// </remarks>
    internal IReadOnlyList<JournalEntry> Steps { get; }
}
