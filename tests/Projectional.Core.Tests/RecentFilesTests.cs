using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The recent list's rules: newest first, no repeats, and a cap.</summary>
/// <remarks>
/// All three are the list's own decision rather than the store's, which is why they are tested here: a store that also
/// de-duplicated would be a second place where "what counts as the same file" is answered.
/// </remarks>
public sealed class RecentFilesTests
{
    [Fact]
    public void TheNewestFileComesFirst()
    {
        var files = RecentFiles.Empty.With(@"C:\a.mps").With(@"C:\b.mps");

        Assert.Equal([@"C:\b.mps", @"C:\a.mps"], files.Paths);
    }

    [Fact]
    public void OpeningTheSameFileAgainMovesItToTheFront()
    {
        var files = RecentFiles.Empty.With(@"C:\a.mps").With(@"C:\b.mps").With(@"C:\a.mps");

        Assert.Equal([@"C:\a.mps", @"C:\b.mps"], files.Paths);
    }

    [Fact]
    public void APathIsTheSameFileWhateverItsCase()
    {
        // Windows does not distinguish them, so neither may the list.
        var files = RecentFiles.Empty.With(@"C:\Work\a.mps").With(@"c:\work\A.MPS");

        Assert.Single(files.Paths);
        Assert.Equal(@"c:\work\A.MPS", files.Paths[0]);
    }

    [Fact]
    public void TheListStopsAtItsCapacity()
    {
        var files = RecentFiles.Empty;

        for (var index = 0; index < RecentFiles.Capacity + 3; index++)
        {
            files = files.With($@"C:\{index}.mps");
        }

        Assert.Equal(RecentFiles.Capacity, files.Paths.Count);
        Assert.Equal($@"C:\{RecentFiles.Capacity + 2}.mps", files.Paths[0]);
    }

    [Fact]
    public void AFileCanBeForgotten()
    {
        var files = RecentFiles.Empty.With(@"C:\a.mps").With(@"C:\b.mps").Without(@"C:\a.mps");

        Assert.Equal([@"C:\b.mps"], files.Paths);
    }

    [Fact]
    public void AStoredListIsCleanedUpWhenItIsRead()
    {
        // A hand-edited file may hold blanks, repeats and too many entries; none of them may reach the menu.
        var files = RecentFiles.From([@"C:\a.mps", "", "  ", @"C:\A.mps", @"C:\b.mps"]);

        Assert.Equal([@"C:\a.mps", @"C:\b.mps"], files.Paths);
    }
}