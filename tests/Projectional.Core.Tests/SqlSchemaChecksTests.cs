using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.SqlSchema.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The schema's own check: two columns of one table under one name.
/// </summary>
/// <remarks>
/// A duplicate across two *different* tables is ordinary - every table has an `id` - so the check walks tables
/// rather than the document, and one of these tests is what says so.
/// </remarks>
public sealed class SqlSchemaChecksTests
{
    [Fact]
    public void TwoColumnsOfOneTableWithOneNameAreReported()
    {
        var table = Table(out var model, "person");
        Column(model, table, "id");
        var second = Column(model, table, "id");

        var finding = Assert.Single(new ColumnNamesCheck().Find(model));

        Assert.Equal(second.Id, finding.Diagnostic.Target);
        Assert.Equal(ColumnNamesCheck.Code, finding.Diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, finding.Diagnostic.Severity);
        Assert.Equal("Rename to \"id2\"", finding.Label);
    }

    [Fact]
    public void TheSameColumnNameInTwoTablesIsQuiet()
    {
        var first = Table(out var model, "person");
        Column(model, first, "id");

        var second = model.NewChild(model.Roots[0], SqlSchemaLanguage.TablesSlot, SqlConceptKeys.Table);
        second.Set("name", "city");
        Column(model, second, "id");

        Assert.Empty(new ColumnNamesCheck().Find(model));
    }

    [Fact]
    public void TheRepairRenamesTheLaterColumnAndRemovesItsFinding()
    {
        var table = Table(out var model, "person");
        Column(model, table, "id");
        var second = Column(model, table, "id");

        using var session = new EditorSession(model.Roots[0], SqlSchemaLanguage.CreateNotations(SqlSchemaLanguage.Concepts));
        var fix = Assert.Single(new ColumnNamesCheck().Find(model));
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("id2", second.GetOrNull("name"));
        Assert.Empty(new ColumnNamesCheck().Find(model));
    }

    [Fact]
    public void TheSetIsTheStandardCataloguePlusThisCheck()
    {
        // The reference group is deliberately absent: this language's keys cross documents on purpose, and a
        // document opened alone would make every one of them look dangling.
        Assert.Equal(
            StandardChecks.Names.Count + StandardChecks.Structure.Count + StandardChecks.Expressions.Count + 1,
            SqlSchemaChecks.All.Count);

        Assert.IsType<ColumnNamesCheck>(SqlSchemaChecks.All.Checks[^1]);
        Assert.DoesNotContain(SqlSchemaChecks.All.Checks, check => check is UnresolvedReferenceCheck);
    }

    private static Node Table(out Model model, string name)
    {
        model = SqlSchemaLanguage.NewProject().NewModel("schema");
        var schema = model.NewNode(SqlConceptKeys.Schema);
        schema.Set("name", "schema");
        model.AddRoot(schema);

        var table = model.NewChild(schema, SqlSchemaLanguage.TablesSlot, SqlConceptKeys.Table);
        table.Set("name", name);

        return table;
    }

    private static Node Column(Model model, Node table, string name)
    {
        var column = model.NewChild(table, SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);
        column.Set("name", name);

        return column;
    }
}
