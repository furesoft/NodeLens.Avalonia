namespace Projectional.Web;

/// <summary>Where the browser shell keeps its documents, behind a seam a test can replace.</summary>
/// <remarks>
/// Two methods and a string key, which is all <c>localStorage</c> is: a synchronous string map. The interface
/// exists because the workspace's save and restore are worth testing without a browser, and because a host that
/// wants a different store - an IndexedDB adapter, a file system in a desktop wrapper - should not have to
/// reimplement the rule about what is written when.
/// </remarks>
public interface IDocumentStorage
{
    /// <summary>The value stored at <paramref name="key"/>, or <see langword="null"/> when there is none.</summary>
    /// <param name="key">The key the workspace chose.</param>
    string? Read(string key);

    /// <summary>Replaces the value stored at <paramref name="key"/>.</summary>
    /// <param name="key">The key the workspace chose.</param>
    /// <param name="value">The content to store.</param>
    void Write(string key, string value);
}
