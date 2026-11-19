using System.Text;

using Projectional.LanguageExport;
using Projectional.Languages;
using Projectional.Workbench;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The shell's half of an export: putting the generated files and the assembly where the user chose.
/// </summary>
/// <remarks>
/// The exporter answers text and bytes and never a path, so the writing rules - create the folder, overwrite rather
/// than append, UTF-8 without a BOM - are the shell's, and they are pinned here instead of in a window.
/// </remarks>
public sealed class LanguageExportWriterTests
{
    [Fact]
    public void TheFilesAndTheAssemblyLandInTheFolderTheUserPicked()
    {
        var result = Export(ExportOutput.Assembly);

        using var directory = new TemporaryDirectory();
        var target = directory.Combine("package");

        var written = LanguageExportWriter.Write(result, target);

        Assert.Equal(3, written.Count);
        Assert.All(written, path => Assert.True(File.Exists(path), path));

        Assert.Equal(result.Files[0].Text, File.ReadAllText(Path.Combine(target, "DeclaredLanguage.cs")));
        Assert.Equal(result.Files[1].Text, File.ReadAllText(Path.Combine(target, "README.md")));
        Assert.Equal(result.Assembly!.Bytes, File.ReadAllBytes(Path.Combine(target, "DeclaredLanguage.dll")));
    }

    [Fact]
    public void TheSourceIsWrittenAsUtf8WithoutABom()
    {
        var result = Export(ExportOutput.Source);

        using var directory = new TemporaryDirectory();

        var path = LanguageExportWriter.Write(result, directory.Root)[0];
        var bytes = File.ReadAllBytes(path);

        // A BOM would make the generated file differ from what the exporter answered, and a reader that trusted the
        // bytes would see a character the language does not have.
        Assert.NotEqual(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Equal(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(result.Files[0].Text), bytes);
    }

    [Fact]
    public void WritingAgainReplacesWhatWasThereInsteadOfAppending()
    {
        var result = Export(ExportOutput.Source);

        using var directory = new TemporaryDirectory();

        var first = LanguageExportWriter.Write(result, directory.Root);
        var second = LanguageExportWriter.Write(result, directory.Root);

        Assert.Equal(first, second);
        Assert.Equal(result.Files[0].Text, File.ReadAllText(second[0]));
    }

    [Fact]
    public void WithoutAnAssemblyOnlyTheFilesAreWritten()
    {
        using var directory = new TemporaryDirectory();

        var written = LanguageExportWriter.Write(Export(ExportOutput.Source), directory.Root);

        Assert.Equal(2, written.Count);
        Assert.DoesNotContain(written, path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }

    private static ExportResult Export(ExportOutput output)
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        return LanguageExporter.Export(language, "Sample.Language", output);
    }
}
