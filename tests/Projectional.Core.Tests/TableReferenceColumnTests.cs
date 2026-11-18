using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A table column that names a reference: the shape a foreign key has.
/// </summary>
/// <remarks>
/// It has its own two-concept language instead of borrowing <c>TestLanguage</c>, because the shared fixture is
/// also declared as a model in the parity tests and a slot added there would have to be added twice - and this
/// language exists to answer one question: does a reference drawn in a table cell behave like a reference drawn
/// anywhere else?
/// </remarks>
public sealed class TableReferenceColumnTests
{
    private const string Container = "R.Container";
    private const string Row = "R.Row";

    private static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(Container, [new ChildSlot("rows", Row)]),
        new ConceptDeclaration(
            Row,
            [
                new PropertySlot("name", PropertyType.String, "row"),
                new ReferenceSlot("link", Row, allowOwnSubtree: true),
            ],
            interfaces: [FrameworkInterfaces.Named]),
    ]);

    private static NotationRegistry Notations { get; } = CreateNotations();

    [Fact]
    public void AReferenceColumnDrawsTheTargetsName()
    {
        var (model, container) = Model();
        var first = RowOf(model, container, "first");
        RowOf(model, container, "second");
        first.SetReference("link", container.GetChildren("rows")[1]);

        Assert.Equal(
            """
            name   link
            first  second
            second <none>
            """,
            LayoutSnapshot.Render(model, Notations, width: 40));
    }

    [Fact]
    public void AReferenceColumnKeepsTheAddressItsRowWouldHave()
    {
        var (model, container) = Model();
        var first = RowOf(model, container, "first");
        var tree = new EditorTreeBuilder(Notations).Build(container);

        var cell = Assert.Single(
            tree.Cells().OfType<ReferenceCell>(),
            reference => reference.Address.Equals(CellAddress.OfSlot(first.Id, "link")));

        Assert.Equal(ReferenceState.Unset, cell.State);
    }

    [Fact]
    public void AnUnsetReferenceColumnSaysWhatTheColumnSays()
    {
        var (model, container) = Model();
        var first = RowOf(model, container, "first");
        RowOf(model, container, "second");

        var cell = Assert.Single(
            new EditorTreeBuilder(Notations).Build(container).Cells().OfType<ReferenceCell>(),
            reference => reference.Address.Equals(CellAddress.OfSlot(first.Id, "link")));

        // The notation's word, not the framework's: a table column already says what an empty cell shows.
        Assert.Equal("<none>", cell.Text);
    }

    private static NotationRegistry CreateNotations()
    {
        var notations = new NotationRegistry(Concepts);

        notations.Add(Row, ed => ed.Line(ed.Property("name")));

        notations.Add(Container, ed => ed.Table(
            "rows",
            ed.Column("name", ed.Keyword("name")),
            ed.Column("link", ed.Keyword("link"), "<none>")));

        return notations;
    }

    private static (Model Model, Node Container) Model()
    {
        var model = new Project(Concepts).NewModel("rows");
        var container = model.NewNode(Container);
        model.AddRoot(container);

        return (model, container);
    }

    private static Node RowOf(Model model, Node container, string name)
    {
        var row = model.NewChild(container, "rows", Row);
        row.Set("name", name);

        return row;
    }
}
