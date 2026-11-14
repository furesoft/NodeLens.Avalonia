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
    public ModelChangedEventArgs(ModelId model, long version, IReadOnlyList<IModelChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Model = model;
        Version = version;
        Changes = changes;
    }

    public ModelId Model { get; }

    /// <summary>The model's <see cref="Model.Version"/> after this set was applied.</summary>
    public long Version { get; }

    /// <summary>The changes in the order they happened.</summary>
    public IReadOnlyList<IModelChange> Changes { get; }
}
