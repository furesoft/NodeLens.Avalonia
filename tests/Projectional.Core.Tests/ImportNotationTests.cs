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
/// literal whose text <em>is</em> a line break draws as one - the sample ends a value with a newline, and what an author
/// reads there is a break, not <c>\n</c>.
/// </remarks>
public sealed class ImportNotationTests
{
    [Fact]
    public void ADeclaredImporterDrawsAsTheRuleListItIs()
    {
        var drawn = Render("ini.mps");

        Assert.Contains("importer ini", drawn, StringComparison.Ordinal);
        Assert.Contains("rule Pipeline.Pipeline", drawn, StringComparison.Ordinal);
        Assert.Contains("capture name from << which class >>", drawn, StringComparison.Ordinal);
        Assert.Contains("repeat stages separated by", drawn, StringComparison.Ordinal);
        Assert.Contains("rule Pipeline.Stage", drawn, StringComparison.Ordinal);
        Assert.Contains("capture command from << which class >>", drawn, StringComparison.Ordinal);
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
