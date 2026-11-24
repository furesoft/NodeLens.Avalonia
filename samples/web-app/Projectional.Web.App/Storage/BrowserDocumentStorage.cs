using Microsoft.JSInterop;

using Projectional.Web;

namespace Projectional.Web.App.Storage;

/// <summary>The workspace's documents, kept in the browser's <c>localStorage</c>.</summary>
/// <remarks>
/// <para>
/// Synchronous on purpose. The workspace's save and restore are ordinary string reads and writes, and a browser is
/// the one host where a synchronous call into the platform is available
/// (<see cref="IJSInProcessRuntime"/>). Modeling them as asynchronous would spread a <c>Task</c> through every
/// command for no gain.
/// </para>
/// <para>
/// The two methods answer rather than throw: a store that is full, disabled in private mode or holding a value
/// from a different version must not take the shell down with it. A read that fails is "nothing stored", and a
/// write that fails leaves the user's document in the tab where they can still download it.
/// </para>
/// </remarks>
public sealed class BrowserDocumentStorage : IDocumentStorage
{
    private readonly IJSInProcessRuntime _js;

    /// <summary>Creates the store over the browser's <c>localStorage</c>.</summary>
    /// <param name="js">The page's JavaScript runtime, which a browser host implements synchronously.</param>
    /// <exception cref="InvalidOperationException">The host cannot call JavaScript synchronously.</exception>
    public BrowserDocumentStorage(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);

        _js = js as IJSInProcessRuntime
            ?? throw new InvalidOperationException(
                "The browser shell persists synchronously, which needs the WebAssembly JavaScript runtime.");
    }

    /// <inheritdoc />
    public string? Read(string key)
    {
        try
        {
            return _js.Invoke<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Write(string key, string value)
    {
        try
        {
            _js.InvokeVoid("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // A full quota is not a reason to lose the document on screen; it is a reason the next reload will not
            // see it, which the status line reports when the user saves.
        }
    }
}
