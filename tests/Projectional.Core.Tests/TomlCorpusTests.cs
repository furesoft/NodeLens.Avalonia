using System.Text;
using Projectional.Semantics;
using Projectional.Toml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The text corpus: a TOML file imported and generated again has to come back byte for byte.</summary>
/// <remarks>
/// <para>
/// Round-tripping over <em>text</em> is the property a reference language promises -
/// <c>generate(import(text))</c> equals <c>text</c> - and it cannot be stated over models, because an import mints fresh
/// node ids (ADR 0004). The files here are the generator's own output, which is the only spelling the comparison can
/// hold: import does not keep a file's layout, because a blank line and an indentation are not constructs TOML says
/// anything about.
/// </para>
/// <para>
/// <strong>Never regenerate one of these files to make a test pass.</strong> A byte that moves is either a language bug
/// or a deliberate change of spelling, and the second one belongs in the commit that made it.
/// </para>
/// </remarks>
public sealed class TomlCorpusTests
{
    private static readonly string CorpusDirectory = Path.Combine(AppContext.BaseDirectory, "Corpus", "Text");

    [Fact]
    public void TheCorpusIsNotEmpty()
    {
        Assert.NotEmpty(Directory.GetFiles(CorpusDirectory, "*.toml"));
    }

    [Fact]
    public void EveryCorpusFileImportsAndGeneratesTheSameText()
    {
        foreach (var path in Directory.GetFiles(CorpusDirectory, "*.toml").Order(StringComparer.Ordinal))
        {
            var stored = Encoding.UTF8.GetString(File.ReadAllBytes(path));

            var model = TomlLanguage.NewProject().NewModel(Path.GetFileName(path));
            var imported = TomlLanguage.Importer.Import(model, stored);

            Assert.True(imported.Succeeded, $"{path}: {string.Join(", ", imported.Diagnostics)}");
            Assert.Empty(imported.Diagnostics);

            var generated = new Generator().Generate(Assert.Single(imported.Roots), TomlTextGenerators.Toml);

            Assert.True(generated.Succeeded, $"{path}: {generated}");
            Assert.Equal(stored, generated.Text);
        }
    }

    [Fact]
    public void ACorpusFileIsPlainUtf8WithoutABom()
    {
        // The comparison above decodes with a plain UTF8Encoding, so a BOM would arrive as a character the importer
        // reports as an unexpected token - this says why it must not be there in the first place.
        foreach (var path in Directory.GetFiles(CorpusDirectory, "*.toml"))
        {
            var bytes = File.ReadAllBytes(path);

            Assert.NotEqual(0xEF, bytes[0]);
            Assert.DoesNotContain((byte)'\r', bytes);
        }
    }
}
