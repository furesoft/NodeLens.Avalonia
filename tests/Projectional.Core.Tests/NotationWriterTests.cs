using Projectional.LanguageExport;
using Projectional.Languages;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The notation half of the export: a declared notation written as the <c>ed.*</c> calls it lowers through.
/// </summary>
/// <remarks>
/// A declared notation lowers through <c>NotationEditor</c>, so writing the same builder call back out is a round
/// trip rather than a translation - which is why an author who has read an editor document recognises the file.
/// </remarks>
public sealed class NotationWriterTests
{
    [Fact]
    public void TheDeclaredSampleNotationsBecomeBuilderCalls()
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "Declared"), out var error);

        Assert.Null(error);
        Assert.NotNull(language);

        var source = new SourceWriter();

        NotationWriter.Write(source, language.Notations.Notations);

        Assert.Equal(
            """
            notations.Add("Pipeline.Pipeline", ed => ed.Sequence(
                ed.Line(ed.Keyword("pipeline"), ed.Space, ed.Property("name")),
                ed.Indent(
                    ed.Table("stages", "+ stage", ed.Column("name", ed.Keyword("stage")), ed.Column("command", ed.Keyword("run")), ed.Column("retries", ed.Keyword("retries")))
                )
            ));
            notations.Add("Pipeline.Stage", ed => ed.Line(ed.Property("name"), ed.Space, ed.Property("command"), ed.Space, ed.Property("retries")));

            """,
            source.ToString(),
            ignoreLineEndingDifferences: true);
    }
}
