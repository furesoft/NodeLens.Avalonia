namespace Projectional.Core;

/// <summary>
/// The transaction a single public edit opens for itself when nothing else opened one.
/// </summary>
/// <remarks>
/// <para>
/// Every mutating method on <see cref="Node"/> and <see cref="Model"/> takes one of these, so that a
/// write is atomic from a subscriber's point of view and so that a write which fails halfway — a
/// <see cref="Node.SetChild"/> whose replacement does not conform, say — is rolled back instead of
/// leaving the model between states.
/// </para>
/// <para>
/// The scope is a no-op when the model already has a transaction (the outer one owns commit and
/// rollback) or when nobody is listening (nothing to publish, nothing to undo), which keeps ordinary
/// editing allocation-free.
/// </para>
/// </remarks>
internal readonly struct MutationScope : IDisposable
{
    private readonly Model? _model;
    private readonly int _mark;
    private readonly long _versionBefore;

    private MutationScope(Model model)
    {
        _model = model;
        _mark = model.JournalCount;
        _versionBefore = model.Version;
        model.BeginImplicitTransaction();
    }

    /// <summary>Opens a scope, or returns the no-op scope if the edit needs no bookkeeping.</summary>
    public static MutationScope Begin(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.IsRecording && !model.HasTransaction ? new MutationScope(model) : default;
    }

    /// <summary>Keeps the edits made inside the scope.</summary>
    public void Dispose() => _model?.EndTransaction(_mark, _versionBefore, commit: true);
}
