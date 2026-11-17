namespace Projectional.Workbench;

/// <summary>Where the list of recently opened files is kept between runs.</summary>
/// <remarks>
/// An interface rather than a file path in a field, because the app is one host of this list and a test is another: a
/// store that is a file makes the list a thing the app tests, and a store that is a list makes it a thing the list
/// tests. What a store must <em>not</em> do is decide anything - de-duplication, ordering and the cap belong to
/// <see cref="RecentFiles"/>.
/// </remarks>
internal interface IRecentFilesStore
{
    /// <summary>The stored paths, in the order they were written.</summary>
    IReadOnlyList<string> Read();

    /// <summary>Replaces what is stored.</summary>
    void Write(IReadOnlyList<string> paths);
}