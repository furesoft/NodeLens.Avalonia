using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The merge verb: its exit codes, which are the contract a git merge driver reads.</summary>
/// <remarks>
/// The three files are the workflow sample, edited the way two branches would: one changes a step's command, the other
/// renames a call. That is the gate in its smallest real form, and it runs the same code the shipped app runs when a
/// hook calls it.
/// </remarks>
public sealed class MergeExportTests
{
    [Fact]
    public void TwoEditsOfDifferentNodesAreMergedAndWritten()
    {
        using var folder = Scenario();

        var exit = MergeExport.Run([
            MergeExport.Switch,
            Path.Combine(AppContext.BaseDirectory, "Workflow"),
            folder.Base,
            folder.Ours,
            folder.Theirs,
            MergeExport.Output,
            folder.Merged]);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(folder.Merged));

        var merged = File.ReadAllText(folder.Merged);

        Assert.Contains("dotnet test --no-build", merged, StringComparison.Ordinal);
        Assert.Contains("rebuild-again", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void AConflictWritesNothingAndAnswersTwo()
    {
        using var folder = Scenario(conflicting: true);

        var exit = MergeExport.Run([
            MergeExport.Switch,
            Path.Combine(AppContext.BaseDirectory, "Workflow"),
            folder.Base,
            folder.Ours,
            folder.Theirs,
            MergeExport.Output,
            folder.Merged]);

        // `2` is what a driver has to answer: the file stays as our side left it, and a person resolves it.
        Assert.Equal(2, exit);
        Assert.False(File.Exists(folder.Merged));
    }

    [Fact]
    public void AFolderThatIsNotALanguageAnswersOne()
    {
        using var folder = Scenario();

        var exit = MergeExport.Run([
            MergeExport.Switch,
            folder.Merged + ".not-a-language",
            folder.Base,
            folder.Ours,
            folder.Theirs,
            MergeExport.Output,
            folder.Merged]);

        Assert.Equal(1, exit);
        Assert.False(File.Exists(folder.Merged));
    }

    [Fact]
    public void ACommandLineWithoutAnOutputIsRefused()
    {
        using var folder = Scenario();

        Assert.Equal(1, MergeExport.Run([MergeExport.Switch, "a", "b", "c", "d"]));
    }

    /// <summary>Three files derived from the workflow sample, and the place a result would go.</summary>
    /// <param name="conflicting">Whether both sides change the same command, which no merge can decide.</param>
    private static Files Scenario(bool conflicting = false)
    {
        var directory = Directory.CreateTempSubdirectory("merge-export");
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Workflow", "release.mps"));

        var ours = source.Replace(">dotnet test<", ">dotnet test --no-build<", StringComparison.Ordinal);

        var theirs = conflicting
            ? source.Replace(">dotnet test<", ">dotnet test -v n<", StringComparison.Ordinal)
            : source.Replace("name=\"rebuild\"", "name=\"rebuild-again\"", StringComparison.Ordinal)
                .Replace(">rebuild<", ">rebuild-again<", StringComparison.Ordinal);

        return new Files(
            directory,
            Write(directory, "base.mps", source),
            Write(directory, "ours.mps", ours),
            Write(directory, "theirs.mps", theirs),
            Path.Combine(directory.FullName, "merged.mps"));
    }

    private static string Write(DirectoryInfo directory, string name, string text)
    {
        var path = Path.Combine(directory.FullName, name);

        File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return path;
    }

    /// <summary>The four paths a run needs, and the folder that holds them.</summary>
    private sealed class Files(DirectoryInfo directory, string baseFile, string ours, string theirs, string merged)
        : IDisposable
    {
        internal string Base => baseFile;

        internal string Ours => ours;

        internal string Theirs => theirs;

        internal string Merged => merged;

        public void Dispose() => directory.Delete(recursive: true);
    }
}
