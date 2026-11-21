using Projectional.Core;
using Projectional.Language;
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

    [Fact]
    public void TheCellsACSharpShapedLanguageNeedsAreWrittenBackOut()
    {
        // The three cells that only a declared language can reach through the editor models: a stacked child list, a
        // list that stays inside the line, and a group of rows that leaves no row while its slot is empty. A cell the
        // writer has no spelling for throws instead of dropping the notation, so this is the check that the export
        // keeps up with the editor language.
        var ed = NotationEditor.Create();
        var notations = new NotationRegistry(
            ConceptRegistry.Create(
            [
                new ConceptDeclaration(
                    "Export.Rule",
                    [
                        new ChildSlot("body", "Export.Body", Cardinality.Many),
                        new ChildSlot("arguments", "Export.Body", Cardinality.Many),
                        new ChildSlot("otherwise", "Export.Body", Cardinality.One),
                    ]),
                new ConceptDeclaration("Export.Body", [new PropertySlot("name", PropertyType.String)], nameSlot: "name"),
            ]));

        notations.Add("Export.Rule", ed => ed.Sequence(
            ed.Children("body", "<no body>", stacked: true),
            ed.Line(ed.Punct("("), ed.InlineCollection("arguments", ed.Punct(",")), ed.Punct(")")),
            ed.WhenFilledBlock("otherwise", ed.Line(ed.Keyword("else")))));

        var source = new SourceWriter();

        NotationWriter.Write(source, notations.Notations);

        Assert.Equal(
            """
            notations.Add("Export.Rule", ed => ed.Sequence(
                ed.Children("body", "<no body>", spacing: 1, stacked: true),
                ed.Line(ed.Punct("("), ed.InlineCollection("arguments", separator: ed.Punct(",")), ed.Punct(")")),
                ed.WhenFilledBlock("otherwise", ed.Line(ed.Keyword("else")))
            ));

            """,
            source.ToString(),
            ignoreLineEndingDifferences: true);
    }
}
