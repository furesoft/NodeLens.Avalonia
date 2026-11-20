namespace Projectional.Workbench;

/// <summary>One row of the project explorer: an open document, or a file of an opened language.</summary>
/// <param name="Text">What the row says - a document's title, a project's name, or a file's path inside it.</param>
/// <param name="Depth">How far the row is indented, in two-space steps.</param>
/// <param name="Document">
/// The open document the row stands for, or <see langword="null"/> for a file that has no tab: pressing a row
/// with one activates it, which is why the row carries the tab rather than a path it would have to look up.
/// </param>
/// <param name="Path">The file the row stands for, or <see langword="null"/> for a group or a project row.</param>
internal sealed record ProjectEntry(string Text, int Depth, DocumentTab? Document = null, string? Path = null)
{
    /// <summary>The row as the explorer draws it, with the star a modified document carries everywhere else.</summary>
    public override string ToString() =>
        new string(' ', Depth * 2) + Text + (Document is { IsDirty: true } ? " *" : string.Empty);
}
