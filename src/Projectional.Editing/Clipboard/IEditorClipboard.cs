namespace Projectional.Editing;

/// <summary>
/// The only thing the editing layer needs from a clipboard: hand a payload over, take one back.
/// </summary>
/// <remarks>
/// <para>
/// The interface carries no toolkit, because the layer that decides what Copy means must not depend on the
/// platform that owns the clipboard. A host adapts it - the Avalonia control does, over
/// <c>TopLevel.Clipboard</c> - and a host without a clipboard at all gets <see cref="InMemoryClipboard"/>,
/// which keeps what it was given and nothing else.
/// </para>
/// <para>
/// It is called <c>IEditorClipboard</c> rather than <c>IClipboard</c> because Avalonia owns that name for its
/// own abstraction, and an adapter is exactly the place where the two would collide.
/// </para>
/// <para>
/// Reading and writing are deliberately synchronous. A key arrives synchronously and an action applies
/// synchronously, so an asynchronous contract would put an <c>await</c> into every action that touches the
/// clipboard. A platform whose clipboard can only be read asynchronously therefore caches, which is what
/// <see cref="RefreshAsync"/> is for: it lets the host pull the platform's text at a moment where waiting
/// costs nothing, such as the editor gaining focus.
/// </para>
/// </remarks>
public interface IEditorClipboard
{
    /// <summary>The text on the clipboard, or <see langword="null"/> when it holds none.</summary>
    /// <remarks>
    /// Text is what an editable cell of a projected document holds, so a single value can be carried between
    /// applications that know nothing about this one.
    /// </remarks>
    string? Text { get; set; }

    /// <summary>
    /// A subtree of a model, or <see langword="null"/> when the clipboard does not hold one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fragment is not text, and that is the whole point: pasting one puts nodes into the model rather than
    /// characters into a cell, which is what makes copying a rule and pasting it into another document work.
    /// It stays inside the process - the wire format that would let another application carry it is a
    /// separate, unimplemented decision ([clipboard.md](../../../docs/architecture/clipboard.md)).
    /// </para>
    /// <para>
    /// A clipboard holds <em>one</em> payload at a time, so writing <see cref="Text"/> clears
    /// <see cref="Fragment"/> and writing <see cref="Fragment"/> clears <see cref="Text"/>. An implementation
    /// that kept both would let a paste act on something the user did not just copy.
    /// </para>
    /// </remarks>
    ModelFragment? Fragment { get; set; }

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
