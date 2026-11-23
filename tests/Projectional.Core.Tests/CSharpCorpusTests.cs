using System.Text;

using Projectional.CSharp.Generation;
using Projectional.LanguageExport;
using Projectional.Semantics;

using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The golden corpus <c>docs/architecture/csharp.md</c> asks for: documents that generate to checked-in <c>.cs</c>
/// files, and every golden compiles.
/// </summary>
/// <remarks>
/// <para>
/// Two levels, because the interesting failures are semantic rather than textual: a byte that moves is a change to the
/// generator and must be reviewed, exactly like the <c>.mps</c> corpus - never regenerate a golden to make a test pass -
/// and compiling the result is the only test that proves the text is C# and not merely stable.
/// </para>
/// <para>
/// The compiler is the one the export uses, with warnings as errors: a golden that would break a host's build breaks
/// here first. That is also why every corpus document is warning-free C# rather than merely valid C#.
/// </para>
/// </remarks>
public sealed class CSharpCorpusTests
{
    private static readonly string CorpusDirectory = Path.Combine(AppContext.BaseDirectory, "Corpus", "CSharp");

    public static TheoryData<string> Names
    {
        get
        {
            var names = new TheoryData<string>();

            foreach (var (name, _) in CSharpCorpus.Documents())
            {
                names.Add(name);
            }

            return names;
        }
    }

    [Fact]
    public void TheCorpusIsNotEmpty() => Assert.NotEmpty(CSharpCorpus.Documents());

    [Theory]
    [MemberData(nameof(Names))]
    public void TheGeneratedTextIsTheCheckedInGolden(string name)
    {
        var generated = Generate(name);
        var stored = File.ReadAllBytes(Path.Combine(CorpusDirectory, $"{name}.cs"));

        Assert.Equal(stored, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(generated));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void EveryGoldenIsPlainLfTextWithoutABomOrATrailingNewline(string name)
    {
        var stored = File.ReadAllBytes(Path.Combine(CorpusDirectory, $"{name}.cs"));

        // A byte order mark or a carriage return would survive a checkout on one platform and not the other, which
        // turns a corpus that looks stable into one that is stable per machine.
        Assert.NotEqual(0xEF, stored[0]);
        Assert.DoesNotContain((byte)'\r', stored);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void EveryGoldenCompiles(string name)
    {
        _ = AssemblyCompiler.Compile($"CSharpCorpus_{name}", Generate(name));
    }

    private static string Generate(string name)
    {
        var model = CSharpCorpus.Documents().Single(document => document.Name == name).Model;

        var result = new Generator().Generate(model.Roots[0], CSharpTextGenerators.Generator);

        Assert.True(result.Succeeded, $"the {name} document generates");

        return result.Text;
    }
}
