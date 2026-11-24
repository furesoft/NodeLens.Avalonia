namespace Projectional.Web;

/// <summary>One document as the workspace's index remembers it.</summary>
/// <param name="Id">The key the document's text is stored under; it survives a rename.</param>
/// <param name="Name">What the tab was called when the workspace was written.</param>
/// <param name="Language">The language it was opened in, by the name the language menu shows.</param>
public sealed record SavedDocument(string Id, string Name, string Language);
