using Projectional.Core;
using Projectional.ImportRules;
using Projectional.Persistence;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>How a declared importer is drawn, pinned on the importers the pipeline sample ships.</summary>
/// <remarks>
/// A notation only earns its place if the document it draws is the one an author already wrote, so both samples are
/// loaded from disk and rendered. The assertions name the lines they mean rather than pinning a whole drawing, because a
/// literal whose text <em>is</em> a line break draws as one - whether a rule spells its break as a literal is the
/// author's choice, and what a reader of the drawing should see is the break either way.
/// </remarks>
public sealed class ImportNotationTests
{
    [Fact]
    public void ADeclaredImporterDrawsAsTheRuleListItIs()
    {
        var drawn = Render("ini.mps");

        Assert.Contains("importer ini", drawn, StringComparison.Ordinal);
        Assert.Contains("rule Pipeline.Pipeline", drawn, StringComparison.Ordinal);
        Assert.Contains("line name", drawn, StringComparison.Ordinal);
        Assert.Contains("repeat stages separated by", drawn, StringComparison.Ordinal);
        Assert.Contains("rule Pipeline.Stage", drawn, StringComparison.Ordinal);
        Assert.Contains("line command", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void AnImporterIsDrawnLikeAnyOtherDefinition()
    {
        // The shape every definition language shares: the page keeps a margin, so the header is one indent in, and
        // what the definition says about itself hangs two indents under it - tokens and rules here, exactly as the
        // settings and sections hang under a concept's header. Nothing pinned this before, which is how the importer
        // came to start at the page edge while a concept did not.
        var lines = Render("ini.mps").Split('\n');

        Assert.Equal("  importer ini", lines[0]);
        Assert.StartsWith("      token Space", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("      rule Pipeline.Pipeline", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("        \"pipeline = \"", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void ATokenClassDrawsAsTheSentenceItMeans()
    {
        var drawn = Render("relaxed.mps");

        Assert.Contains("token Text stands between \" and resolves \\", drawn, StringComparison.Ordinal);
        Assert.Contains("token Comment is skipped", drawn, StringComparison.Ordinal);
        Assert.Contains("capture name from Text", drawn, StringComparison.Ordinal);
    }

    private static string Render(string file) =>
        LayoutSnapshot.Render(
            ModelFile.Load(
                new Project(ImportLanguage.Concepts),
                Path.Combine(AppContext.BaseDirectory, "Declared", "import", file)).Roots[0],
            ImportLanguage.Notations,
            width: 200);
}
