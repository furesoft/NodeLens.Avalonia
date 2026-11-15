using System.Runtime.InteropServices;

using Avalonia.Input.Platform;

using Projectional.Editing;

namespace Projectional.Avalonia;

/// <summary>
/// The platform clipboard of the window the editor lives in, cached so that a synchronous edit can read it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ITextClipboard"/> is synchronous because a key arrives synchronously; a platform clipboard is
/// asynchronous because another process owns it. The adapter bridges the two by keeping the last text it
/// saw: a write stores the text and hands it to the platform in the background, a read answers from the
/// cache, and <see cref="RefreshAsync"/> pulls what the platform really holds at the moments where waiting
/// costs nothing - the editor gaining focus, and just before a paste is applied.
/// </para>
/// <para>
/// The clipboard is resolved lazily through a function rather than captured in the constructor, because a
/// control has no top level until it is attached to one, and the same adapter has to survive re-attachment.
/// </para>
/// <para>
/// Both directions swallow a failing platform call. A clipboard is shared with every other process on the
/// machine, so an application holding it open is normal rather than exceptional, and an editor that threw
/// because Word had the clipboard locked would lose the keystroke as well as the copy.
/// </para>
/// </remarks>
public sealed class AvaloniaClipboard : ITextClipboard
{
    private readonly Func<IClipboard?> _clipboard;

    private string? _text;

    /// <summary>Creates the adapter over whatever clipboard <paramref name="clipboard"/> returns.</summary>
    /// <param name="clipboard">
    /// Returns the platform clipboard, or <see langword="null"/> while there is none - the control passes
    /// <c>() =&gt; TopLevel.GetTopLevel(this)?.Clipboard</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="clipboard"/> is <see langword="null"/>.</exception>
    public AvaloniaClipboard(Func<IClipboard?> clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        _clipboard = clipboard;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Reading answers the cache, which <see cref="RefreshAsync"/> keeps current. Writing updates the cache
    /// first and then the platform, so a Copy followed immediately by a Paste inside this application works
    /// even while the background write is still running.
    /// </remarks>
    public string? Text
    {
        get => _text;

        set
        {
            _text = value;

            if (value is not null)
            {
                _ = PushAsync(value);
            }
        }
    }

    /// <inheritdoc/>
    public async Task RefreshAsync()
    {
        if (_clipboard() is not { } clipboard)
        {
            return;
        }

        try
        {
            _text = await clipboard.TryGetTextAsync().ConfigureAwait(true);
        }
        catch (ExternalException)
        {
            // The platform clipboard is held open by another process; the cached text stays usable.
        }
        catch (InvalidOperationException)
        {
            // The control was detached from its window between the call and the answer.
        }
    }

    private async Task PushAsync(string text)
    {
        if (_clipboard() is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);

            // Without the flush the text only lives in this process, and a copied value would be gone
            // from the platform clipboard the moment the user closes the editor.
            await clipboard.FlushAsync().ConfigureAwait(true);
        }
        catch (ExternalException)
        {
            // As above: the copy stays in this application's cache, which is where Paste reads it.
        }
        catch (InvalidOperationException)
        {
            // The control was detached from its window between the call and the answer.
        }
    }
}
