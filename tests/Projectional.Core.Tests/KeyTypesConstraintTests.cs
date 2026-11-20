using Projectional.Core;
using Projectional.Samples.SqlSchema;
using Projectional.Samples.SqlSchema.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The schema's own first type rule: a key whose two columns could never be matched up.</summary>
/// <remarks>
/// This is the finding the schema's types aspect exists for. Without it the model says nothing: both reference
/// slots accept any column of any table, so a key from an <c>int</c> column to a <c>text</c> one looks exactly like
/// a correct one.
/// </remarks>
public sealed class KeyTypesConstraintTests
{
    [Fact]
    public void AKeyBetweenTwoDifferentColumnTypesIsReported()
    {
        var model = Schema(out var numbers, out var words);

        var key = model.NewChild(numbers, SqlSchemaLanguage.KeysSlot, SqlConceptKeys.ForeignKey);

        key.SetReference(SqlSchemaLanguage.LocalSlot, ColumnOf(numbers, "id"));
        key.SetReference(SqlSchemaLanguage.TargetSlot, ColumnOf(words, "label"));

        var finding = Assert.Single(Check(model));

        Assert.Equal(key.Id, finding.Target);
        Assert.Equal(SqlSchemaLanguage.LocalSlot, finding.Role);
        Assert.Equal(KeyTypesConstraint.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
        Assert.Contains("\"label\"", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyBetweenTwoColumnsOfOneTypeIsNotReported()
    {
        var model = Schema(out var numbers, out var words);

        var key = model.NewChild(numbers, SqlSchemaLanguage.KeysSlot, SqlConceptKeys.ForeignKey);

        key.SetReference(SqlSchemaLanguage.LocalSlot, ColumnOf(numbers, "id"));
        key.SetReference(SqlSchemaLanguage.TargetSlot, ColumnOf(words, "count"));

        Assert.Empty(Check(model));
    }

    [Fact]
    public void AKeyToAColumnOfAnInterchangeableTypeIsNotReported()
    {
        // The language declares `decimal` over the same primitive as `int`, so this direction is the widening the
        // type layer can express; asking for one *type* would refuse it.
        var model = Schema(out var numbers, out var words);

        var key = model.NewChild(numbers, SqlSchemaLanguage.KeysSlot, SqlConceptKeys.ForeignKey);

        key.SetReference(SqlSchemaLanguage.LocalSlot, ColumnOf(numbers, "id"));
        key.SetReference(SqlSchemaLanguage.TargetSlot, ColumnOf(words, "amount"));

        Assert.Empty(Check(model));
    }

    [Fact]
    public void AKeyWithAnUnresolvedSideIsNotReported()
    {
        // The reference layer reports what it cannot resolve, and a second finding about the same missing column
        // would be one mistake reported twice.
        var model = Schema(out var numbers, out var words);

        var key = model.NewChild(numbers, SqlSchemaLanguage.KeysSlot, SqlConceptKeys.ForeignKey);

        key.SetReference(SqlSchemaLanguage.TargetSlot, ColumnOf(words, "label"));

        Assert.Empty(Check(model));
    }

    private static IReadOnlyList<Diagnostic> Check(Model model) =>
        new ConstraintEngine(SqlSchemaLanguage.Constraints, types: SqlSchemaLanguage.Types).Check(model);

    /// <summary>Two tables: one of whole numbers, one of text and amounts.</summary>
    private static Model Schema(out Node numbers, out Node words)
    {
        var model = SqlSchemaLanguage.NewProject().NewModel("schema");
        var schema = model.NewNode(SqlConceptKeys.Schema);

        schema.Set("name", "schema");
        model.AddRoot(schema);

        numbers = model.NewChild(schema, SqlSchemaLanguage.TablesSlot, SqlConceptKeys.Table);
        numbers.Set("name", "numbers");

        words = model.NewChild(schema, SqlSchemaLanguage.TablesSlot, SqlConceptKeys.Table);
        words.Set("name", "words");

        var id = model.NewChild(numbers, SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);
        id.Set("name", "id");
        id.Set("type", "int");

        var count = model.NewChild(words, SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);
        count.Set("name", "count");
        count.Set("type", "int");

        var label = model.NewChild(words, SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);
        label.Set("name", "label");
        label.Set("type", "text");

        var amount = model.NewChild(words, SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);
        amount.Set("name", "amount");
        amount.Set("type", "decimal");

        return model;
    }

    private static Node ColumnOf(Node table, string name) =>
        table.GetChildren(SqlSchemaLanguage.ColumnsSlot).Single(column => column.GetOrNull("name") == name);
}
