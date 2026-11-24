using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using Projectional.Web;

namespace Projectional.Web.App.Shell;

/// <summary>The page's JavaScript, behind one object so the component holds no interop plumbing.</summary>
/// <remarks>
/// The module under <c>js/editor.js</c> owns three jobs a Blazor component cannot do itself: reading a font's real
/// advance, swallowing a key before the browser acts on it, and saving a file. Everything else - the projection,
/// the caret, the edits - is .NET and stays testable headlessly.
/// </remarks>
public sealed class EditorInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    /// <summary>Creates the seam over the page's JavaScript runtime.</summary>
    /// <param name="js">The runtime the host provides.</param>
    public EditorInterop(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);

        _js = js;
    }

    /// <summary>The measured size of one layout unit.</summary>
    /// <param name="reference">The element whose computed font is the editor's.</param>
    public async Task<CellMetrics> MeasureAsync(ElementReference reference)
    {
        var module = await ModuleAsync();
        var values = await module.InvokeAsync<double[]>("measure", reference);

        return values is { Length: 2 } ? new CellMetrics(values[0], values[1]) : CellMetrics.Default;
    }

    /// <summary>Binds the key and text listeners to <paramref name="composer"/>.</summary>
    /// <param name="composer">The hidden text area that receives typed text and the keys the editor handles.</param>
    /// <param name="reference">What the page calls back into.</param>
    /// <param name="chords">Every chord the page must swallow.</param>
    public async Task AttachAsync<T>(
        ElementReference composer,
        DotNetObjectReference<T> reference,
        IReadOnlyList<string> chords)
        where T : class
    {
        var module = await ModuleAsync();

        await module.InvokeVoidAsync("attach", composer, reference, chords);
    }

    /// <summary>Gives the hidden text area the focus, which is what makes typing reach the editor.</summary>
    /// <param name="composer">The hidden text area.</param>
    public async Task FocusAsync(ElementReference composer)
    {
        var module = await ModuleAsync();

        await module.InvokeVoidAsync("focusComposer", composer);
    }

    /// <summary>Scrolls the painted caret into view inside the editor's scroll container.</summary>
    /// <param name="host">The element the editor is painted in.</param>
    public async Task ScrollCaretIntoViewAsync(ElementReference host)
    {
        var module = await ModuleAsync();

        await module.InvokeVoidAsync("scrollCaretIntoView", host);
    }

    /// <summary>Writes a document to the user's downloads.</summary>
    /// <param name="name">The file name, extension included.</param>
    /// <param name="text">The document's stored form.</param>
    public async Task DownloadAsync(string name, string text)
    {
        var module = await ModuleAsync();

        await module.InvokeVoidAsync("download", name, text);
    }

    /// <summary>Puts text on the operating system's clipboard, inside the gesture that asked for it.</summary>
    /// <param name="text">What to copy.</param>
    public async Task WriteClipboardAsync(string text)
    {
        var module = await ModuleAsync();

        await module.InvokeVoidAsync("writeClipboard", text);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("detach");
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // A page that navigated away has no listeners left to remove.
        }

        _module = null;
    }

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/editor.js");
}
