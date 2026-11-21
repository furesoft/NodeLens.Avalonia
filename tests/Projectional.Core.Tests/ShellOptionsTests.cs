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
        Assert.Null(defaults.Dialogs);
    }

    [Fact]
    public async Task AHostThatSuppliesItsOwnDialogsAnswersTheShellsQuestions()
    {
        // The dialogs are the other half of the environment seam: a host whose files live somewhere else still has to
        // be able to say which one the user picked. The contract speaks paths and a choice enum, never a toolkit type,
        // which is what makes this implementation - one that answers from a script instead of a window - possible.
        var dialogs = new ScriptedDialogs
        {
            Folder = "/chosen/folder",
            Open = "/chosen/model.mps",
            Save = "/chosen/out.txt",
            Unsaved = UnsavedChoice.Discard,
        };

        var options = new ShellOptions { Dialogs = dialogs };

        Assert.Same(dialogs, options.Dialogs);
        Assert.Equal("/chosen/folder", await dialogs.ChooseFolderAsync("Pick one"));
        Assert.Equal("/chosen/model.mps", await dialogs.ChooseFileToOpenAsync(
            "Open",
            new ShellFileType("Model", ["*.mps"])));
        Assert.Equal("/chosen/out.txt", await dialogs.ChooseFileToSaveAsync(
            "Save",
            "out",
            new ShellFileType("Text", ["*.txt"]),
            "txt"));
        Assert.Equal(UnsavedChoice.Discard, await dialogs.AskUnsavedAsync("Rules"));

        // Declining is one answer, not two: null means the user went away, whichever way they did it.
        dialogs.Folder = null;

        Assert.Null(await dialogs.ChooseFolderAsync("Pick one"));
    }

    /// <summary>A host's dialogs: answers from a script instead of a window.</summary>
    private sealed class ScriptedDialogs : IShellDialogs
    {
        public string? Folder { get; set; }

        public string? Open { get; set; }

        public string? Save { get; set; }

        public UnsavedChoice Unsaved { get; set; } = UnsavedChoice.Cancel;

        public Task<string?> ChooseFolderAsync(string title) => Task.FromResult(Folder);

        public Task<string?> ChooseFileToOpenAsync(string title, ShellFileType type) => Task.FromResult(Open);

        public Task<string?> ChooseFileToSaveAsync(
            string title,
            string suggestedName,
            ShellFileType type,
            string? defaultExtension = null) => Task.FromResult(Save);

        public Task<UnsavedChoice> AskUnsavedAsync(string title) => Task.FromResult(Unsaved);
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
