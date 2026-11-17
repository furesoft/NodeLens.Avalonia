using System.Runtime.InteropServices;

using Avalonia.Input.Platform;

using Projectional.Editing;

namespace Projectional.Avalonia;

/// <summary>
/// The platform clipboard of the window the editor lives in, cached so that a synchronous edit can read it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IEditorClipboard"/> is synchronous because a key arrives synchronously; a platform clipboard is
/// asynchronous because another process owns it. The adapter bridges the two by keeping the last text it
/// saw: a write stores the text and hands it to the platform in the background, a read answers from the
/// cache, and <see cref="RefreshAsync"/> pulls what the platform really holds at the moments where waiting
/// costs nothing - the editor gaining focus, and just before a paste is applied.
/// </para>
/// <para>
/// The fragment never leaves the process, so it is kept here and nowhere else: the platform cannot carry it,
/// and pretending otherwise would lose it. A refresh that finds text the editor did not write therefore drops
/// the fragment - another application has since put something on the clipboard, and pasting the editor's
/// older fragment would be pasting what the user no longer has.
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
public sealed class AvaloniaClipboard : IEditorClipboard
{
    private readonly Func<IClipboard?> _clipboard;

    private string? _text;
    private string? _written;
    private ModelFragment? _fragment;

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
            _written = value;
            _fragment = null;

            if (value is not null)
            {
                _ = PushAsync(value);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Writing a fragment clears the cached text, because the clipboard carries one payload. The platform's
    /// text is not erased - it is not ours to erase, and it may well be what another application put there - so
    /// it comes back the next time <see cref="RefreshAsync"/> reads it, and the fragment goes with it.
    /// </remarks>
    public ModelFragment? Fragment
    {
        get => _fragment;

        set
        {
            _fragment = value;
            _text = null;
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
            var current = await clipboard.TryGetTextAsync().ConfigureAwait(true);

            if (!string.Equals(current, _written, StringComparison.Ordinal))
            {
                // Somebody else wrote to the clipboard since this editor did, so the fragment it holds is no
                // longer what the user copied last.
                _fragment = null;
            }

            // While a fragment is held the payload is that fragment, and the platform's text is somebody
            // else's leftover - reading it into the cache would make Paste look like it had a choice.
            if (_fragment is null)
            {
                _text = current;
            }
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
