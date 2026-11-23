using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The projection step turns a model node and its concept's notation into a cell tree. What the tests
/// pin down is the mapping (notation cell kind → runtime cell kind), where addresses come from, and
/// the cases that must not throw: an empty slot, a missing target, a concept without notation.
/// </summary>
public sealed class EditorTreeTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);

    [Fact]
    public void TheTreeRootMirrorsTheNotationRoot()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var root = Assert.IsType<ContainerCell>(Builder.Build(rule).Root);

        Assert.Equal(CellOrientation.Vertical, root.Orientation);
        Assert.False(root.IsIndented);
        Assert.Equal(3, root.Content.Count);
    }

    [Fact]
    public void ALineBecomesAHorizontalContainerAndAnIndentAVerticalOne()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var root = Builder.Build(rule).Root;

        var line = Assert.IsType<ContainerCell>(root.Children[0]);
        var indent = Assert.IsType<ContainerCell>(root.Children[1]);

        Assert.Equal(CellOrientation.Horizontal, line.Orientation);
        Assert.False(line.IsIndented);
        Assert.Equal(CellOrientation.Vertical, indent.Orientation);
        Assert.True(indent.IsIndented);
        Assert.Equal(2, indent.Content.Count);
    }

    [Fact]
    public void TheLineShowsTheNotationsConstantsSpacesSlotsAndReferences()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var line = Assert.IsType<ContainerCell>(Builder.Build(rule).Root.Children[0]);

        Assert.Collection(
            line.Content,
            cell =>
            {
                var keyword = Assert.IsType<ConstantCell>(cell);
                Assert.Equal("rule", keyword.Text);
                Assert.Equal(ConstantKind.Keyword, keyword.Kind);
            },
            cell => Assert.IsType<SpaceCell>(cell),
            cell =>
            {
                var property = Assert.IsType<PropertyCell>(cell);
                Assert.Equal("name", property.Slot);
                Assert.Equal("unnamed", property.Text);
                Assert.True(property.IsSet);
                Assert.Same(PropertyType.String, property.Type);
            },
            cell => Assert.IsType<SpaceCell>(cell),
            cell => Assert.Equal("as", Assert.IsType<ConstantCell>(cell).Text),
            cell => Assert.IsType<SpaceCell>(cell),
            cell =>
            {
                var reference = Assert.IsType<ReferenceCell>(cell);
                Assert.Equal("tag", reference.Slot);
                Assert.Equal(ReferenceState.Unset, reference.State);
                Assert.Equal("<none>", reference.Text);
                Assert.Null(reference.Target);
            },
            cell => Assert.IsType<SpaceCell>(cell),
            cell =>
            {
                var brace = Assert.IsType<ConstantCell>(cell);
                Assert.Equal("{", brace.Text);
                Assert.Equal(ConstantKind.Punctuation, brace.Kind);
            });
    }

    [Fact]
    public void AnUnsetPropertyIsShownAsEmptyText()
    {
        var step = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        var line = Builder.Build(step).Root;
        var index = Assert.IsType<PropertyCell>(line.Children[0]);

        Assert.Equal("index", index.Slot);
        Assert.Equal(string.Empty, index.Text);
        Assert.False(index.IsSet);
        Assert.Same(PropertyType.Integer, index.Type);
    }

    [Fact]
    public void AReferenceShowsTheTargetsName()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var tag = model.NewNode(TestLanguage.Tag);
        tag.Set("label", "core");

        rule.SetReference("tag", tag);

        var reference = Find<ReferenceCell>(rule, "tag");

        Assert.Equal(ReferenceState.Resolved, reference.State);
        Assert.Equal("core", reference.Text);
        Assert.Equal(tag.Id, reference.Target);
    }

    [Fact]
    public void AReferenceToAMissingNodeIsMarkedUnresolved()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var missing = NodeId.New();
        rule.SetReference("tag", missing);

        var reference = Find<ReferenceCell>(rule, "tag");

        Assert.Equal(ReferenceState.Unresolved, reference.State);
        Assert.Equal("<unresolved>", reference.Text);
        Assert.Equal(missing, reference.Target);
    }

    [Fact]
    public void AReferenceToANamelessConceptFallsBackToTheConceptKey()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Source", [new ReferenceSlot("to", "Test.Anon")]),
            new ConceptDeclaration("Test.Anon", [new PropertySlot("label", PropertyType.String)]),
        ]);
        var notations = new NotationRegistry(concepts);
        notations.Add("Test.Source", ed => ed.Line(ed.Reference("to")));
        notations.Add("Test.Anon", ed => ed.Line(ed.Property("label")));

        var model = new Project(concepts).NewModel("Anon");
        var source = model.NewNode("Test.Source");
        source.SetReference("to", model.NewNode("Test.Anon"));

        var reference = Assert.IsType<ReferenceCell>(
            new EditorTreeBuilder(notations).Build(source).Root.Children[0]);

        Assert.Equal(ReferenceState.Resolved, reference.State);
        Assert.Equal("<Test.Anon>", reference.Text);
    }

    [Fact]
    public void AReferenceToANodeNamedInCodeSaysWhereTheNameLives()
    {
        // A declared language is data and cannot execute anything (ADR 0014), so a concept named by C# has no function
        // the editor could call. The fallback used to be the concept key, which reads like "this node has no name" - and
        // the name does exist, in an assembly this layer never loads. Saying so is the honest answer, and it invents no
        // data: the text is derived, so it can never be written into a file.
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.Source", [new ReferenceSlot("to", "Test.Stop")]),
            new ConceptDeclaration("Test.Stop", [new PropertySlot("label", PropertyType.String)], nameIsExternal: true),
        ]);
        var notations = new NotationRegistry(concepts);
        notations.Add("Test.Source", ed => ed.Line(ed.Reference("to")));
        notations.Add("Test.Stop", ed => ed.Line(ed.Property("label")));

        var model = new Project(concepts).NewModel("Stop");
        var source = model.NewNode("Test.Source");
        var stop = model.NewNode("Test.Stop");

        source.SetReference("to", stop);

        var reference = Assert.IsType<ReferenceCell>(
            new EditorTreeBuilder(notations).Build(source).Root.Children[0]);

        Assert.Equal(ReferenceState.Resolved, reference.State);
        Assert.Equal("<Test.Stop: named in code>", reference.Text);

        // The document has no name to give: the marker is a statement about where the name lives, not a name. Nothing
        // is answered, so nothing can be persisted as a fallback.
        Assert.True(stop.Concept.NameIsExternal);
        Assert.Null(stop.Name);
    }

    [Fact]
    public void AResolvedReferenceCellNeedsATarget()
    {
        var address = CellAddress.OfSlot(NodeId.New(), "to");

        Assert.Throws<ProjectionException>(() =>
            new ReferenceCell(address, "to", "x", ReferenceState.Resolved));
    }

    [Fact]
    public void AChildSlotHoldsTheChildsOwnProjectionAtAnElementAddress()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        var step = model.NewChild(body, "steps", TestLanguage.Step);
        step.Set("text", "ship it");

        var tree = Builder.Build(rule);
        var child = Find<ChildCell>(rule, "body");

        Assert.Same(body, tree.Source.GetChild("body"));
        Assert.False(child.IsEmpty);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "body"), child.Address);
        Assert.Single(child.Content);

        var bodyRoot = Assert.IsType<ContainerCell>(child.Content[0]);
        Assert.Equal(CellAddress.OfElement(rule.Id, "body", 0), bodyRoot.Address);
        Assert.Same(child, bodyRoot.Parent);

        // Below the child's root, addresses belong to the child itself, not to the slot it was reached by.
        var steps = bodyRoot.Children[0];
        Assert.Equal(CellAddress.OfSlot(body.Id, "steps"), steps.Address);
        Assert.Equal(CellAddress.OfElement(body.Id, "steps", 0), steps.Children[0].Address);
    }

    [Fact]
    public void AChildSlotPutsItsSpacingBetweenItsChildren()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        model.NewChild(body, "steps", TestLanguage.Step).Set("text", "one");
        model.NewChild(body, "steps", TestLanguage.Step).Set("text", "two");

        var steps = Find<ChildCell>(body, "steps");

        Assert.Equal(3, steps.Content.Count);
        Assert.IsType<SpaceCell>(steps.Content[1]);
        Assert.NotSame(steps.Content[0], steps.Content[2]);
    }

    [Fact]
    public void AChildSlotWithoutSpacingPutsItsChildrenSideBySide()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.List", [new ChildSlot("items", "Test.Item")]),
            new ConceptDeclaration("Test.Item", [new PropertySlot("text", PropertyType.String)]),
        ]);
        var notations = new NotationRegistry(concepts);
        notations.Add("Test.List", ed => ed.Children("items", spacing: 0));
        notations.Add("Test.Item", ed => ed.Line(ed.Property("text")));

        var model = new Project(concepts).NewModel("List");
        var list = model.NewNode("Test.List");
        model.NewChild(list, "items", "Test.Item");
        model.NewChild(list, "items", "Test.Item");

        var children = Assert.IsType<ChildCell>(new EditorTreeBuilder(notations).Build(list).Root);

        Assert.Equal(2, children.Content.Count);
        Assert.DoesNotContain(children.Content, cell => cell is SpaceCell);
    }

    [Fact]
    public void AnEmptyChildSlotHasNoContentAndKeepsItsEmptyText()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var child = Find<ChildCell>(rule, "body");

        Assert.True(child.IsEmpty);
        Assert.Empty(child.Content);
        Assert.Equal("<empty>", child.EmptyText);
    }

    [Fact]
    public void ACollectionInterleavesItsItemsWithSeparators()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);

        for (var i = 0; i < 3; i++)
        {
            model.NewChild(body, "notes", TestLanguage.Note);
        }

        var collection = Find<CollectionCell>(body, "notes");

        Assert.Equal(3, collection.Items.Count);
        Assert.Equal(2, collection.Separators.Count);
        Assert.Equal(1, collection.Spacing);
        Assert.Equal(7, collection.Content.Count);
        Assert.Equal(";", Assert.IsType<ConstantCell>(collection.Content[1]).Text);
        Assert.IsType<SpaceCell>(collection.Content[2]);
        Assert.All(collection.Separators, separator => Assert.IsType<ConstantCell>(separator));
    }

    [Fact]
    public void ACollectionWithoutSeparatorIsSeparatedByItsSpacing()
    {
        var concepts = ConceptRegistry.Create(
        [
            new ConceptDeclaration("Test.List", [new ChildSlot("items", "Test.Item")]),
            new ConceptDeclaration("Test.Item", [new PropertySlot("text", PropertyType.String)]),
        ]);
        var notations = new NotationRegistry(concepts);
        notations.Add("Test.List", ed => ed.Collection("items"));
        notations.Add("Test.Item", ed => ed.Line(ed.Property("text")));

        var model = new Project(concepts).NewModel("List");
        var list = model.NewNode("Test.List");
        model.NewChild(list, "items", "Test.Item");
        model.NewChild(list, "items", "Test.Item");

        var collection = Assert.IsType<CollectionCell>(new EditorTreeBuilder(notations).Build(list).Root);

        Assert.Empty(collection.Separators);
        Assert.Equal(3, collection.Content.Count);
        Assert.IsType<SpaceCell>(collection.Content[1]);
    }

    [Fact]
    public void ACollectionWithNoSpacingPutsItsItemsSideBySide()
    {
        var address = CellAddress.OfSlot(NodeId.New(), "items");
        var first = new ConstantCell(address, "a", ConstantKind.Punctuation);
        var second = new ConstantCell(address, "b", ConstantKind.Punctuation);

        var collection = new CollectionCell(address, "items", "<empty>", [first, second], [], spacing: 0);

        Assert.Equal(0, collection.Spacing);
        Assert.Equal([first, second], collection.Content);
    }

    [Fact]
    public void ACollectionWithAWiderSpacingRepeatsTheSpace()
    {
        var address = CellAddress.OfSlot(NodeId.New(), "items");
        var first = new ConstantCell(address, "a", ConstantKind.Punctuation);
        var second = new ConstantCell(address, "b", ConstantKind.Punctuation);
        var separator = new ConstantCell(address, ";", ConstantKind.Punctuation);

        var collection = new CollectionCell(address, "items", "<empty>", [first, second], [separator], spacing: 2);

        Assert.Equal(5, collection.Content.Count);
        Assert.Same(separator, collection.Content[1]);
        Assert.All([2, 3], index => Assert.IsType<SpaceCell>(collection.Content[index]));
        Assert.Same(second, collection.Content[4]);
    }

    [Fact]
    public void ACollectionWithoutTrailingGapEndsWithItsLastItem()
    {
        var address = CellAddress.OfSlot(NodeId.New(), "items");
        var first = new ConstantCell(address, "a", ConstantKind.Punctuation);
        var second = new ConstantCell(address, "b", ConstantKind.Punctuation);

        var collection = new CollectionCell(address, "items", "<empty>", [first, second], []);

        Assert.Same(second, collection.Content[^1]);
    }

    [Fact]
    public void ACollectionRejectsANegativeSpacing()
    {
        var address = CellAddress.OfSlot(NodeId.New(), "items");
        var first = new ConstantCell(address, "a", ConstantKind.Punctuation);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CollectionCell(address, "items", "<empty>", [first], [], spacing: -1));
    }

    [Fact]
    public void AnEmptyCollectionHasNoSeparators()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);

        var collection = Find<CollectionCell>(body, "notes");

        Assert.True(collection.IsEmpty);
        Assert.Empty(collection.Items);
        Assert.Empty(collection.Separators);
        Assert.Empty(collection.Content);
    }

    [Fact]
    public void AConceptWithoutNotationBecomesAMarkerInsteadOfFailing()
    {
        var tag = TestLanguage.NewModel().NewNode(TestLanguage.Tag);

        var marker = Assert.IsType<MissingNotationCell>(Builder.Build(tag).Root);

        Assert.Equal(TestLanguage.Tag, marker.ConceptKey);
        Assert.Equal(new CellAddress(tag.Id), marker.Address);
    }

    [Fact]
    public void ContainersCarryTheNodeAddressAndSlotCellsTheSlotAddress()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var tree = Builder.Build(rule);
        var root = Assert.IsType<ContainerCell>(tree.Root);

        Assert.Equal(new CellAddress(rule.Id), root.Address);
        Assert.Equal(new CellAddress(rule.Id), Assert.IsType<ConstantCell>(root.Children[0].Children[0]).Address);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "name"), root.Children[0].Children[2].Address);
        Assert.Equal(CellAddress.OfSlot(rule.Id, "tag"), root.Children[0].Children[6].Address);
    }

    [Fact]
    public void FindReturnsTheOutermostCellAtAnAddress()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        model.NewChild(rule, "body", TestLanguage.Body);

        var tree = Builder.Build(rule);

        Assert.Same(tree.Root, tree.Find(new CellAddress(rule.Id)));
        Assert.IsType<ChildCell>(tree.Find(CellAddress.OfSlot(rule.Id, "body")));
        Assert.IsType<ContainerCell>(tree.Find(CellAddress.OfElement(rule.Id, "body", 0)));
        Assert.Null(tree.Find(CellAddress.OfSlot(rule.Id, "nosuchslot")));
    }

    [Fact]
    public void CellsAreWalkedInDocumentOrderFromTheRoot()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var tree = Builder.Build(rule);
        var cells = tree.Cells().ToArray();

        Assert.Same(tree.Root, cells[0]);
        Assert.Same(tree.Root.Children[0], cells[1]);
        Assert.Same(tree.Root.Children[0].Children[0], cells[2]);
        Assert.Equal(cells.Length, cells.Distinct().Count());
    }

    [Fact]
    public void EveryCellExceptTheRootKnowsItsParent()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var body = model.NewChild(rule, "body", TestLanguage.Body);
        model.NewChild(body, "steps", TestLanguage.Step);
        model.NewChild(body, "notes", TestLanguage.Note);

        var tree = Builder.Build(rule);

        Assert.All(tree.Cells(), cell =>
        {
            if (ReferenceEquals(cell, tree.Root))
            {
                Assert.Null(cell.Parent);
                return;
            }

            Assert.NotNull(cell.Parent);
            Assert.Contains(cell, cell.Parent.Children);
        });
    }

    [Fact]
    public void TheSameCellCannotBePlacedTwice()
    {
        var address = new CellAddress(NodeId.New());
        var cell = new ConstantCell(address, "x", ConstantKind.Keyword);

        Assert.Throws<ProjectionException>(() =>
            new ContainerCell(address, CellOrientation.Horizontal, cell, cell));
    }

    [Fact]
    public void ACollectionRejectsSeparatorsForOnlySomeGaps()
    {
        var address = CellAddress.OfSlot(NodeId.New(), "items");
        var firstSeparator = new ConstantCell(address, ";", ConstantKind.Punctuation);
        var secondSeparator = new ConstantCell(address, ";", ConstantKind.Punctuation);

        var twoItems = new EditorCell[]
        {
            new ConstantCell(address, "a", ConstantKind.Punctuation),
            new ConstantCell(address, "b", ConstantKind.Punctuation),
        };
        var threeItems = new EditorCell[]
        {
            new ConstantCell(address, "a", ConstantKind.Punctuation),
            new ConstantCell(address, "b", ConstantKind.Punctuation),
            new ConstantCell(address, "c", ConstantKind.Punctuation),
        };

        Assert.Throws<ProjectionException>(() =>
            new CollectionCell(address, "items", "<empty>", threeItems, [firstSeparator]));

        Assert.Throws<ProjectionException>(() =>
            new CollectionCell(address, "items", "<empty>", twoItems, [firstSeparator, secondSeparator]));
    }

    private static T Find<T>(Node node, string slot)
        where T : EditorCell
    {
        var tree = Builder.Build(node);
        var address = CellAddress.OfSlot(node.Id, slot);

        return Assert.IsType<T>(tree.Find(address));
    }
}
