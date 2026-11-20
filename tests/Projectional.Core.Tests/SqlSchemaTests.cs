using System.Text;
using Projectional.Core;
using Projectional.Language;
using Projectional.Persistence;
using Projectional.Projection;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.SqlSchema.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The third worked example: two documents of one schema, and a key in one that names a column of the other.
/// </summary>
/// <remarks>
/// This is the sample that found the cross-model reference rule: `Node.SetReference` compared the *model* of the
/// target, so a link between two documents of one project was impossible - while the scope provider already walked
/// up to the project to offer exactly those nodes. The rule compares the project now, and the corpus here is what
/// proves the two halves agree.
/// </remarks>
public sealed class SqlSchemaTests
{
    private static readonly string CorpusDirectory = Path.Combine(AppContext.BaseDirectory, "SqlSchema");

    [Fact]
    public void BothDocumentsSaveBackByteIdentically()
    {
        foreach (var name in (string[])["shop.mps", "orders.mps"])
        {
            var stored = File.ReadAllBytes(Path.Combine(CorpusDirectory, name));

            var model = ModelXml.Deserialize(SqlSchemaLanguage.NewProject(), Encoding.UTF8.GetString(stored));

            Assert.Equal(stored, Encoding.UTF8.GetBytes(ModelXml.Serialize(model)));
        }
    }

    [Fact]
    public void BothDocumentsAreCleanUnderTheirOwnChecks()
    {
        foreach (var name in (string[])["shop.mps", "orders.mps"])
        {
            Assert.Empty(SqlSchemaChecks.All.Find(Load(name)));
        }
    }

    [Fact]
    public void ATableIsDrawnAsAGridOfItsColumnsWithItsKeysUnderIt()
    {
        Assert.Equal(
            """
            schema shop {
              table person {
                column  type
                id      int
                name    text
                city_id int
                foreign key city_id references id
              }
              table city {
                column type
                id     int
                name   text
                << ... >>
              }
            }
            """,
            LayoutSnapshot.Render(
                Load("shop.mps").Roots[0],
                SqlSchemaLanguage.CreateNotations(SqlSchemaLanguage.Concepts),
                width: 60));
    }

    [Fact]
    public void TheGeneratedDdlNamesTheColumnsItsKeysPointAt()
    {
        var generated = new Generator().Generate(Load("shop.mps").Roots[0], SqlSchemaTextGenerators.Ddl);

        Assert.True(generated.Succeeded, generated.Failure?.Message);

        // A table with no keys ends at `);`: the line break in front of the keys is written only when there is one,
        // which is what `WhenFilledPart` is for and why the choice was never the language's to make.
        const string Expected =
            "create table person (\n" +
            "  id int,\n" +
            "  name text,\n" +
            "  city_id int\n" +
            ");\n" +
            "  foreign key (city_id) references id\n" +
            "create table city (\n" +
            "  id int,\n" +
            "  name text\n" +
            ");";

        Assert.Equal(Expected, generated.Text);
    }

    [Fact]
    public void AKeyResolvesIntoAnotherDocument()
    {
        // The two documents are one project, which is the only way a key can name a column of another file.
        var project = SqlSchemaLanguage.NewProject();
        var shop = Load(project, "shop.mps");
        var orders = Load(project, "orders.mps");

        var key = orders.Roots[0]
            .GetChildren(SqlSchemaLanguage.TablesSlot)[0]
            .GetChildren(SqlSchemaLanguage.KeysSlot)[0];

        var target = key.GetReferenceTarget(SqlSchemaLanguage.TargetSlot);

        Assert.NotNull(target);
        Assert.NotSame(shop, orders);

        // The target really is a node of the *other* document, not a coincidentally named one.
        Assert.Contains(target, shop.Nodes);
        Assert.DoesNotContain(target, orders.Nodes);
    }

    [Fact]
    public void AKeySurvivesTheOtherDocumentBeingAbsent()
    {
        // Loading one document alone is the ordinary case - and the reference degrades to unresolved rather than
        // throwing, which is what ADR 0004 promises.
        var orders = Load("orders.mps");
        var key = orders.Roots[0]
            .GetChildren(SqlSchemaLanguage.TablesSlot)[0]
            .GetChildren(SqlSchemaLanguage.KeysSlot)[0];

        Assert.Null(key.GetReferenceTarget(SqlSchemaLanguage.TargetSlot));
        Assert.NotNull(key.GetReference(SqlSchemaLanguage.TargetSlot));
    }

    private static Model Load(string name) => Load(SqlSchemaLanguage.NewProject(), name);

    private static Model Load(Project project, string name) =>
        ModelXml.Deserialize(project, File.ReadAllText(Path.Combine(CorpusDirectory, name)));
}
