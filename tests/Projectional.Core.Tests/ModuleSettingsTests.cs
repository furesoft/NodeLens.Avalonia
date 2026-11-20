using System.Text;

using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A module's settings are a small file the author can open, so the reader has to be tolerant and the writer has to be
/// predictable: what it wrote once is what it reads back, and a line it does not understand costs that line rather than
/// the file.
/// </summary>
public sealed class ModuleSettingsTests
{
    [Fact]
    public void AModuleThatHasNotWrittenSettingsHasWrittenNothing()
    {
        var settings = ModuleSettings.Default;

        Assert.Equal(0, settings.LanguageVersion);
        Assert.Equal(0, settings.ModuleVersion);
        Assert.Empty(settings.DocumentationOutput);
    }

    [Fact]
    public void TheSettingsAreReadFromTheKeysTheyAreWrittenWith()
    {
        var settings = ModuleSettings.From(
        [
            "language.version = 3",
            "module.version = 7",
            "documentation.output = /tmp/doc_gen",
        ]);

        Assert.Equal(3, settings.LanguageVersion);
        Assert.Equal(7, settings.ModuleVersion);
        Assert.Equal("/tmp/doc_gen", settings.DocumentationOutput);
    }

    [Fact]
    public void ALineThisVersionDoesNotKnowIsSkippedRatherThanRefused()
    {
        // A file written by one version and read by the next: an unfamiliar key and a comment cost only themselves.
        var settings = ModuleSettings.From(
        [
            "# written by something else",
            "export = true",
            string.Empty,
            "not a setting at all",
            "module.version = 2",
        ]);

        Assert.Equal(2, settings.ModuleVersion);
        Assert.Equal(0, settings.LanguageVersion);
    }

    [Fact]
    public void AVersionThatIsNotANumberIsTheDefault()
    {
        var settings = ModuleSettings.From(
        [
            "language.version = several",
            "module.version = -2",
        ]);

        Assert.Equal(ModuleSettings.DefaultVersion, settings.LanguageVersion);
        Assert.Equal(ModuleSettings.DefaultVersion, settings.ModuleVersion);
    }

    [Fact]
    public void WhatTheWriterWritesIsWhatTheReaderReads()
    {
        var settings = new ModuleSettings(4, 9, "generated/docs");

        Assert.Equal(settings, ModuleSettings.From(settings.ToLines()));
    }

    [Fact]
    public void AStoreWithNoFileAnswersTheDefaults()
    {
        using var temporary = new TemporaryDirectory();

        Assert.Equal(ModuleSettings.Default, new ModuleSettingsStore(temporary.Root).Read());
    }

    [Fact]
    public void AStoreWritesUtf8WithoutABomAndWithLfEndings()
    {
        using var temporary = new TemporaryDirectory();
        var store = new ModuleSettingsStore(temporary.Root);

        Assert.True(store.TryWrite(new ModuleSettings(1, 2, "/doc"), out var error), error);

        var bytes = File.ReadAllBytes(store.SettingsPath);

        Assert.NotEmpty(bytes);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal(new ModuleSettings(1, 2, "/doc"), store.Read());
    }

    [Fact]
    public void AStoreReadsWhatAUserHandEdited()
    {
        using var temporary = new TemporaryDirectory();
        var store = new ModuleSettingsStore(temporary.Root);

        File.WriteAllText(store.SettingsPath, "# mine\nmodule.version=5\n", Encoding.UTF8);

        Assert.Equal(5, store.Read().ModuleVersion);
        Assert.Equal(0, store.Read().LanguageVersion);
    }

    [Fact]
    public void AStoreThatCannotWriteSaysSoInsteadOfThrowing()
    {
        using var temporary = new TemporaryDirectory();
        var store = new ModuleSettingsStore(Path.Combine(temporary.Root, "gone"));

        Assert.False(store.TryWrite(new ModuleSettings(1, 1, string.Empty), out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
