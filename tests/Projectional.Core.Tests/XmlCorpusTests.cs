using System.Text;
using Projectional.Core;
using Projectional.Semantics;
using Projectional.Xml;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The text corpus: an XML file imported and generated again has to come back byte for byte.</summary>
/// <remarks>
/// The files are the generator's own output, because a model has one spelling for a tree where XML has two: an empty
/// element is written in the long form, and a value is escaped canonically. Everything a document says about its
/// <em>content</em> survives - whitespace between markup, attributes in order, comments, CDATA, namespace prefixes - and
/// that is what these files pin.
/// </remarks>
public sealed class XmlCorpusTests
{
    private static readonly string CorpusDirectory = Path.Combine(AppContext.BaseDirectory, "Corpus", "Text");

    [Fact]
    public void TheCorpusIsNotEmpty()
    {
        Assert.NotEmpty(Directory.GetFiles(CorpusDirectory, "*.xml"));
    }

    [Fact]
    public void EveryCorpusFileImportsAndGeneratesTheSameText()
    {
        foreach (var path in Directory.GetFiles(CorpusDirectory, "*.xml").Order(StringComparer.Ordinal))
        {
            var stored = Encoding.UTF8.GetString(File.ReadAllBytes(path));

            var model = XmlLanguage.NewProject().NewModel(Path.GetFileName(path));
            var imported = XmlLanguage.Importer.Import(model, stored);

            Assert.True(imported.Succeeded, $"{path}: {string.Join(", ", imported.Diagnostics)}");
            Assert.Empty(imported.Diagnostics);
            Assert.Equal(imported.Roots.Count, model.Roots.Count);

            var generated = string.Concat(imported.Roots.Select(root => Generate(root, path)));

            Assert.Equal(stored, generated);
        }
    }

    private static string Generate(Node root, string path)
    {
        var generated = new Generator().Generate(root, XmlTextGenerators.Xml);

        Assert.True(generated.Succeeded, $"{path}: {generated}");
        return generated.Text;
    }
}
