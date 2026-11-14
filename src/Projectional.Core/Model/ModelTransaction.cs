namespace Projectional.Core;

/// <summary>
/// A range of edits that reaches subscribers as one <see cref="ModelChangedEventArgs"/> and that can still
/// be taken back while it is open.
/// </summary>
/// <remarks>
/// <para>
/// Transactions nest. Only the outermost one publishes, so a library that wraps its work in a
/// transaction still collapses into a single event when the caller wrapped it too.
/// </para>
/// <para>
/// Disposing without <see cref="Complete"/> rolls the model back to the state it had when the
/// transaction opened, which is what makes a failed bulk edit leave nothing behind.
/// </para>
/// </remarks>
public sealed class ModelTransaction : IDisposable
{
    private readonly Model _model;
    private readonly int _mark;
    private readonly long _versionBefore;
    private bool _closed;

    internal ModelTransaction(Model model, int mark, long versionBefore)
    {
        _model = model;
        _mark = mark;
        _versionBefore = versionBefore;
    }

    /// <summary>Ends the transaction and keeps its edits.</summary>
    /// <exception cref="ObjectDisposedException">The transaction was already ended.</exception>
    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        _closed = true;
        _model.EndTransaction(_mark, _versionBefore, commit: true);
    }

    /// <summary>Ends the transaction and undoes its edits, unless <see cref="Complete"/> was called.</summary>
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _model.EndTransaction(_mark, _versionBefore, commit: false);
    }
}
