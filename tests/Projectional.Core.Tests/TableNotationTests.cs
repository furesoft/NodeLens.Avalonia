using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// What a table cell and a column refuse, and where they refuse it.
/// </summary>
/// <remarks>
/// The checks split in two on purpose: a column and a table can see their own arguments, so they throw
/// <see cref="ArgumentException"/> where they are built, while the slots a column names belong to
/// another concept and can only be checked once the notation is registered - which is what
/// <see cref="NotationException"/> is for.
/// </remarks>
public sealed class TableNotationTests
{
    private const string Container = "T.Container";
    private const string Item = "T.Item";

    private static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(Container, [new ChildSlot("items", Item)]),
        new ConceptDeclaration(
            Item,
            [
                new PropertySlot("name", PropertyType.String),
                new ChildSlot("one", Item, Cardinality.One),
                new ChildSlot("many", Item),
            ]),
    ]);

    [Fact]
    public void ATableNeedsAColumn()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => NotationEditor.Create().Table("items"));

        Assert.Equal("columns", exception.ParamName);
    }

    [Fact]
    public void ATableRefusesTwoColumnsForTheSameSlot()
    {
        var exception = Assert.Throws<ArgumentException>(() => NotationEditor.Create().Table(
            "items",
            NotationEditor.Create().Column("name", NotationEditor.Create().Keyword("name")),
            NotationEditor.Create().Column("name", NotationEditor.Create().Keyword("again"))));

        Assert.Equal("columns", exception.ParamName);
    }

    [Fact]
    public void AColumnHeaderIsTextTheNotationWroteDown()
    {
        var exception = Assert.Throws<ArgumentException>(() => NotationEditor.Create().Column(
            "name",
            NotationEditor.Create().Property("name")));

        Assert.Equal("header", exception.ParamName);
    }

    [Fact]
    public void ATableOverASlotTheConceptDoesNotDeclareIsRejected()
    {
        var exception = Assert.Throws<NotationException>(() => Register(ed => ed.Table(
            "nope",
            ed.Column("name", ed.Keyword("name")))));

        Assert.Contains("declares no such child", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AColumnTheRowConceptDoesNotDeclareIsRejected()
    {
        var exception = Assert.Throws<NotationException>(() => Register(ed => ed.Table(
            "items",
            ed.Column("nope", ed.Keyword("nope")))));

        Assert.Contains("neither a property nor a child", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AColumnOverAManyValuedChildIsRejected()
    {
        var exception = Assert.Throws<NotationException>(() => Register(ed => ed.Table(
            "items",
            ed.Column("many", ed.Keyword("many")))));

        Assert.Contains("many-valued", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AColumnOverASingleChildIsAccepted()
    {
        var notations = Register(ed => ed.Table("items", ed.Column("one", ed.Keyword("one"))));

        Assert.NotNull(notations);
    }

    [Fact]
    public void ATableCellRefusesRowsOfDifferentWidths()
    {
        var exception = Assert.Throws<ProjectionException>(() => new TableCell(
            Address(),
            "items",
            "<empty>",
            [Header()],
            [[Header(), Header()]]));

        Assert.Contains("where the table has", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATableCellRefusesHeadersWithoutRows()
    {
        var exception = Assert.Throws<ProjectionException>(
            () => new TableCell(Address(), "items", "<empty>", [Header()], []));

        Assert.Contains("shows its placeholder", exception.Message, StringComparison.Ordinal);
    }

    private static CellAddress Address() =>
        CellAddress.OfSlot(TestLanguage.NewModel().NewNode(TestLanguage.Note).Id, "items");

    private static ConstantCell Header() => new(Address(), "name", ConstantKind.Keyword);

    private static NotationRegistry Register(Func<NotationEditor, BlockNotationCell> notation)
    {
        var notations = new NotationRegistry(Concepts);

        notations.Add(Container, notation);

        return notations;
    }
}
