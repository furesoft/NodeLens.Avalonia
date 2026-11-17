using System.Text;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The file the recent list is kept in: one path per line, and silence when it cannot be used.</summary>
/// <remarks>
/// The silence is the interesting part. A missing file is an empty list, an unreadable file is an empty list, and a
/// write that fails leaves the caller's list alone - because a shell that refused to start over its own convenience
/// file would be losing real work to a menu.
/// </remarks>
public sealed class TextRecentFilesStoreTests
{
    [Fact]
    public void TheListRoundTripsThroughTheFile()
    {
        using var directory = new TemporaryDirectory();

        var store = new TextRecentFilesStore(directory.Combine("recent.txt"));

        store.Write([@"C:\a.mps", @"C:\b.mps"]);

        Assert.Equal([@"C:\a.mps", @"C:\b.mps"], store.Read());
    }

    [Fact]
    public void AMissingFileIsAnEmptyList()
    {
        using var directory = new TemporaryDirectory();

        var store = new TextRecentFilesStore(directory.Combine("never-written.txt"));

        Assert.Empty(store.Read());
    }

    [Fact]
    public void AFileWithBlankLinesReadsThemBackVerbatim()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("recent.txt");

        File.WriteAllText(path, "C:\\a.mps\n\nC:\\b.mps\n", new UTF8Encoding(false));

        var store = new TextRecentFilesStore(path);

        // The store does not tidy; RecentFiles.From does, so validation lives in one place only.
        Assert.Equal([@"C:\a.mps", string.Empty, @"C:\b.mps"], store.Read());
        Assert.Equal([@"C:\a.mps", @"C:\b.mps"], RecentFiles.From(store.Read()).Paths);
    }

    [Fact]
    public void ADirectoryThatDoesNotExistYetIsCreated()
    {
        using var directory = new TemporaryDirectory();

        var store = new TextRecentFilesStore(Path.Combine(directory.Root, "nested", "deeper", "recent.txt"));

        store.Write([@"C:\a.mps"]);

        Assert.Single(store.Read());
    }
}