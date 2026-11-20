using Projectional.Core;
using Projectional.Language;
using Projectional.Samples.SqlSchema;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>What a column means as a type, which is the schema's own types aspect.</summary>
public sealed class SqlSchemaTypesTests
{
    [Theory]
    [InlineData("int", "int")]
    [InlineData("text", "text")]
    [InlineData("bool", "bool")]
    [InlineData("date", "date")]
    [InlineData("decimal", "decimal")]
    public void AColumnIsTheTypeItsPropertyNames(string declared, string expected)
    {
        var column = Column(declared, out _);

        Assert.Equal(expected, SqlSchemaLanguage.Types.TypeOf(column).Name);
    }

    [Fact]
    public void TwoColumnsOfOneTypeAreOneType()
    {
        // One instance per value is what makes the answer about the *type* rather than about the node: a key
        // compares two columns, and two nodes cannot be one node.
        var first = Column("int", out var model);
        var second = model.NewChild(model.Roots[0].GetChildren(SqlSchemaLanguage.TablesSlot)[0], SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);

        second.Set("name", "b");
        second.Set("type", "int");

        Assert.Same(SqlSchemaLanguage.Types.TypeOf(first), SqlSchemaLanguage.Types.TypeOf(second));
    }

    [Fact]
    public void AColumnAlwaysHasOneOfTheFiveTypes()
    {
        // The slot declares `int` as its default, so a column that has not had its type chosen is a whole number
        // rather than a hole. That is what lets the key rule treat every column as typed.
        var column = Column(null, out _);

        Assert.Equal("int", SqlSchemaLanguage.Types.TypeOf(column).Name);
    }

    [Fact]
    public void TheModelRefusesAColumnTypeItDoesNotOffer()
    {
        // The refusal lives where the value is written, which is why the type rule's fallback branch is unreachable
        // rather than a case the rule has to answer.
        var column = Column("int", out _);

        Assert.Throws<ModelInvariantException>(() => column.Set("type", "blob"));
    }

    [Fact]
    public void IntAndDecimalAreInterchangeable()
    {
        // The documented boundary: the framework has four primitives and SQL has five column types, so `decimal`
        // leans on `int`. This is what the key rule rests on, so it is pinned rather than left implicit.
        var whole = Column("int", out var model);
        var fixedPoint = model.NewChild(model.Roots[0].GetChildren(SqlSchemaLanguage.TablesSlot)[0], SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);

        fixedPoint.Set("name", "b");
        fixedPoint.Set("type", "decimal");

        var wholeType = SqlSchemaLanguage.Types.TypeOf(whole);
        var fixedPointType = SqlSchemaLanguage.Types.TypeOf(fixedPoint);

        Assert.True(wholeType.IsComparableTo(fixedPointType));
        Assert.True(fixedPointType.IsComparableTo(wholeType));
    }

    [Fact]
    public void TwoDifferentColumnTypesAreNotInterchangeable()
    {
        var number = Column("int", out var model);
        var text = model.NewChild(model.Roots[0].GetChildren(SqlSchemaLanguage.TablesSlot)[0], SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);

        text.Set("name", "b");
        text.Set("type", "text");

        Assert.False(SqlSchemaLanguage.Types.TypeOf(number).IsComparableTo(SqlSchemaLanguage.Types.TypeOf(text)));
    }

    /// <summary>One column in one table, which is the smallest schema a type can be asked about.</summary>
    private static Node Column(string? type, out Model model)
    {
        model = SqlSchemaLanguage.NewProject().NewModel("schema");

        var schema = model.NewNode(SqlConceptKeys.Schema);

        schema.Set("name", "schema");
        model.AddRoot(schema);

        var table = model.NewChild(schema, SqlSchemaLanguage.TablesSlot, SqlConceptKeys.Table);

        table.Set("name", "t");

        var column = model.NewChild(table, SqlSchemaLanguage.ColumnsSlot, SqlConceptKeys.Column);

        column.Set("name", "a");

        if (type is not null)
        {
            column.Set("type", type);
        }

        return column;
    }
}
