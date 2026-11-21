using Projectional.Avalonia;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The seam ADR 0015 puts between the shell and the machine it runs on.</summary>
/// <remarks>
/// An interface nobody can replace is a comment. These tests use a host-shaped implementation - one that keeps the
/// bytes in a dictionary instead of a file - so the claim being checked is the one the ADR makes: the shell asks its
/// storage, and a host may answer.
/// </remarks>
public sealed class ShellOptionsTests
{
    [Fact]
    public void AHostThatSuppliesItsOwnStorageIsTheOneTheShellWritesTo()
    {
        var storage = new InMemoryStorage();
        var store = new Projectional.Workbench.TextRecentFilesStore(storage, "/state/recent.txt");

        store.Write(["one.mps", "two.mps"]);

        // The dictionary, not the file system, holds it - and the shell's own path is what the host keys on.
        Assert.Equal(["one.mps", "two.mps"], storage.Content("/state/recent.txt"));
        Assert.Equal(["one.mps", "two.mps"], store.Read());
    }

    [Fact]
    public void TheOptionsCarryEveryDecisionAHostMakesInOneValue()
    {
        var storage = new InMemoryStorage();

        // Two decisions today and one seam: a host that wants a locked-down editor says so in the same value, rather
        // than by hiding entries after the shell has been built.
        var options = new ShellOptions
        {
            Capabilities = ShellCapabilities.Embedded,
            InitialModel = "/models/hello.mps",
            Storage = storage,
        };

        Assert.Equal(ShellCapabilities.Embedded, options.Capabilities);
        Assert.Equal("/models/hello.mps", options.InitialModel);
        Assert.Same(storage, options.Storage);

        // The default is the standalone editor over the file system, so a host that says nothing gets the app.
        var defaults = new ShellOptions();

        Assert.Equal(ShellCapabilities.Standalone, defaults.Capabilities);
        Assert.Null(defaults.InitialModel);
        Assert.Same(FileShellStorage.Default, defaults.Storage);
    }

    /// <summary>A host's storage: a dictionary instead of a file, which is the whole point of the seam.</summary>
    private sealed class InMemoryStorage : IShellStorage
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _files = new(StringComparer.Ordinal);

        public IReadOnlyList<string> ReadLines(string path) =>
            _files.TryGetValue(path, out var lines) ? lines : [];

        public bool WriteLines(string path, IReadOnlyList<string> lines)
        {
            _files[path] = [.. lines];

            return true;
        }

        public IReadOnlyList<string> Content(string path) => ReadLines(path);
    }
}
