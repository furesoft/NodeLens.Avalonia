using System.IO.Compression;
using System.Text;

using Projectional.LanguageExport;
using Projectional.Languages;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The archive a language is handed on as: which half of an export it holds, and that it is reproducible.</summary>
/// <remarks>
/// The subject is the dependent sample - <c>declared-invoice</c> extends <c>Units</c> - because a package is only worth
/// packing when it is the whole chain: one file per language is the promise the export makes, and the archive is where
/// that promise is delivered.
/// </remarks>
public sealed class LanguagePackageTests
{
    [Fact]
    public void TheSourcesArchiveHoldsEachLanguageAndOneReadme()
    {
        var result = LanguageExporter.Export(Invoice(), "Sample.Invoice");

        var zip = LanguagePackage.Zip(result, PackageContent.Sources);

        Assert.Equal(["UnitsLanguage.cs", "InvoiceLanguage.cs", "README.md"], Names(zip));

        // Each file keeps the namespace the export chose for its language, which is what makes them compile side by side.
        Assert.Contains("namespace Units;", Text(zip, "UnitsLanguage.cs"), StringComparison.Ordinal);
        Assert.Contains("namespace Sample.Invoice;", Text(zip, "InvoiceLanguage.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAssembliesArchiveHoldsOneAssemblyPerLanguage()
    {
        var result = LanguageExporter.Export(Invoice(), "Sample.Invoice", ExportOutput.Assembly);

        var zip = LanguagePackage.Zip(result, PackageContent.Assemblies);

        Assert.Equal(["UnitsLanguage.dll", "InvoiceLanguage.dll"], Names(zip));

        // The bytes in the archive are the ones the export compiled, not a second compilation.
        Assert.Equal(result.Assemblies[0].Bytes, Bytes(zip, "UnitsLanguage.dll"));
        Assert.Equal(result.Assemblies[1].Bytes, Bytes(zip, "InvoiceLanguage.dll"));
    }

    [Fact]
    public void TheSameExportIsTheSameArchive()
    {
        // The export promises that the same language twice is the same bytes; an archive carrying the clock would
        // break that promise at the last step, where it is easiest to miss.
        var result = LanguageExporter.Export(Invoice(), "Sample.Invoice", ExportOutput.Assembly);

        Assert.Equal(
            LanguagePackage.Zip(result, PackageContent.Assemblies),
            LanguagePackage.Zip(result, PackageContent.Assemblies));
    }

    [Fact]
    public void PackagingAssembliesThatWereNeverCompiledSaysSo()
    {
        // Asking for sources is no compile, so the archive of assemblies would be empty - and an empty archive is the
        // one answer a user would take for a successful package.
        var result = LanguageExporter.Export(Invoice(), "Sample.Invoice");

        var exception = Assert.Throws<InvalidOperationException>(() => LanguagePackage.Zip(result, PackageContent.Assemblies));

        Assert.Contains("ExportOutput.Assembly", exception.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Names(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);

        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    private static byte[] Bytes(byte[] zip, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        using var entry = archive.GetEntry(name)!.Open();
        using var buffer = new MemoryStream();

        entry.CopyTo(buffer);

        return buffer.ToArray();
    }

    private static string Text(byte[] zip, string name) => Encoding.UTF8.GetString(Bytes(zip, name));

    /// <summary>The dependent sample, with the base it declares resolved.</summary>
    private static LanguageProject Invoice()
    {
        var units = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Units"), out var unitsError);

        Assert.Null(unitsError);
        Assert.NotNull(units);

        var invoice = LanguageProject.Open(
            Path.Combine(AppContext.BaseDirectory, "Invoice"),
            new ResolvedLanguages(units),
            out var error);

        Assert.Null(error);
        Assert.NotNull(invoice);

        return invoice;
    }
}
