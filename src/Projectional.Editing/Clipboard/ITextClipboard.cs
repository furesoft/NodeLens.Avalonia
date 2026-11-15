namespace Projectional.Editing;

/// <summary>
/// The only thing the editing layer needs from a clipboard: hand text over, take text back.
/// </summary>
/// <remarks>
/// <para>
/// The interface is one property and no toolkit, because the layer that decides what Copy means must not
/// depend on the platform that owns the clipboard. A host adapts it - the Avalonia control does, over
/// <c>TopLevel.Clipboard</c> - and a host without a clipboard at all gets <see cref="InMemoryClipboard"/>,
/// which keeps a string and nothing else.
/// </para>
/// <para>
/// Reading and writing are deliberately synchronous. A key arrives synchronously and an action applies
/// synchronously, so an asynchronous contract would put an <c>await</c> into every action that touches
/// text. A platform whose clipboard can only be read asynchronously therefore caches, which is what
/// <see cref="RefreshAsync"/> is for: it lets the host pull the platform's text at a moment where waiting
/// costs nothing, such as the editor gaining focus.
/// </para>
/// </remarks>
public interface ITextClipboard
{
    /// <summary>The text on the clipboard, or <see langword="null"/> when it holds none.</summary>
    /// <remarks>
    /// Setting it replaces whatever was there; setting <see langword="null"/> empties the clipboard. Text
    /// is the whole vocabulary, because text is what an editable cell of a projected document holds.
    /// </remarks>
    string? Text { get; set; }

    /// <summary>Re-reads the clipboard from wherever it really lives.</summary>
    /// <returns>A task that completes once <see cref="Text"/> is as current as the platform allows.</returns>
    /// <remarks>
    /// A clipboard that lives in memory has nothing to re-read, so the default does nothing. An adapter
    /// over a platform clipboard overrides this and caches what it found, so the next synchronous read -
    /// a Ctrl+V - sees what another application put there.
    /// <para>
    /// This returns a <see cref="Task"/> rather than a <see cref="ValueTask"/> on purpose. The callers
    /// that cannot await - a focus handler, a pointer handler, a menu that opens synchronously - fire and
    /// forget, and a discarded <see cref="ValueTask"/> is an analyzer error because it may be pooled. A
    /// task can be dropped without pretending anything was consumed.
    /// </para>
    /// </remarks>
    Task RefreshAsync() => Task.CompletedTask;
}
