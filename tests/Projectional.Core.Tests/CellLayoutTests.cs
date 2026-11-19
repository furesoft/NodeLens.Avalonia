using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Layout turns a cell tree into positions. The tests pin down the three rules the arrangement is
/// built on - inline cells advance to the right, stacked cells advance downwards, and a cell that
/// stacks inside a line pushes what follows onto its last line - plus the lookups that drawing and
/// hit testing are expressed with.
/// </summary>
/// <remarks>
/// Everything here is measured by <see cref="MonospaceTextMeasure"/>, so one layout unit is one
/// character and the expected numbers can be counted off the notation by hand. That is the whole
/// point of the measure seam: the same expectations would be unreadable in pixels.
/// </remarks>
public sealed class CellLayoutTests
{
    private static readonly EditorTreeBuilder Builder = new(TestNotations.Registry);
    private static readonly CellLayouter Layouter = new(MonospaceTextMeasure.Instance);

    [Fact]
    public void ATextThatStayedOnOneLineIsAnsweredWithoutBeingStored()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var layout = Layouter.Layout(Builder.Build(rule));
        var texts = layout.Cells.Where(placed => placed.Cell is TextCell).ToArray();

        Assert.NotEmpty(texts);

        foreach (var placed in texts)
        {
            var lines = layout.LinesOf(placed.Cell);

            Assert.NotNull(lines);
            Assert.True(lines.IsSingleLine);
            Assert.Equal(placed.Bounds.Height, lines.Height);

            // A property that shows a placeholder instead of its empty value is drawn as wide as the
            // stand-in, so its bounds are wider than the value they describe - and that is the one case
            // where the two numbers may differ.
            if (placed.Cell is not PropertyCell { Text.Length: 0, EmptyText.Length: > 0 })
            {
                Assert.Equal(placed.Bounds.Width, lines.Width);
            }
        }
    }

    [Fact]
    public void TheRootIsPlacedAtTheOriginAndTheSizeCoversIt()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var layout = Layouter.Layout(Builder.Build(rule));

        AssertRect(0, 0, 24, 4, Bounds(layout, layout.Tree.Root));
        Assert.Equal(24, layout.Size.Width);
        Assert.Equal(4, layout.Size.Height);
    }

    [Fact]
    public void InlineCellsAdvanceToTheRightByTheirMeasuredWidth()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var tree = Builder.Build(rule);
        var line = tree.Root.Children[0];

        var layout = Layouter.Layout(tree);

        // rule ␣ unnamed ␣ as ␣ <none> ␣ {
        AssertRect(0, 0, 4, 1, Bounds(layout, line.Children[0]));
        AssertRect(4, 0, 1, 1, Bounds(layout, line.Children[1]));
        AssertRect(5, 0, 7, 1, Bounds(layout, line.Children[2]));
        AssertRect(12, 0, 1, 1, Bounds(layout, line.Children[3]));
        AssertRect(13, 0, 2, 1, Bounds(layout, line.Children[4]));
        AssertRect(15, 0, 1, 1, Bounds(layout, line.Children[5]));
        AssertRect(16, 0, 6, 1, Bounds(layout, line.Children[6]));
        AssertRect(22, 0, 1, 1, Bounds(layout, line.Children[7]));
        AssertRect(23, 0, 1, 1, Bounds(layout, line.Children[8]));
    }

    [Fact]
    public void AStackedContainerPutsEachLineOnItsOwnRowAndIsAsWideAsItsWidest()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var tree = Builder.Build(rule);
        var root = tree.Root;

        var layout = Layouter.Layout(tree);

        AssertRect(0, 0, 24, 1, Bounds(layout, root.Children[0]));
        AssertRect(0, 1, 17, 2, Bounds(layout, root.Children[1]));
        AssertRect(0, 3, 1, 1, Bounds(layout, root.Children[2]));
    }

    [Fact]
    public void AnIndentPushesItsContentRightByOneIndentationLevel()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var tree = Builder.Build(rule);
        var indent = tree.Root.Children[1];

        var layout = Layouter.Layout(tree);

        Assert.Equal(2, MonospaceTextMeasure.Indent);

        // extends ␣ <empty>
        AssertRect(2, 1, 7, 1, Bounds(layout, indent.Children[0]));
        AssertRect(2, 2, 15, 1, Bounds(layout, indent.Children[1]));
    }

    [Fact]
    public void AStackedChildInsideALineMakesTheLineTallerAndWhatFollowsContinuesOnItsLastLine()
    {
        var model = TestLanguage.NewModel();
        var inner = model.NewNode(TestLanguage.Rule);
        var outer = model.NewNode(TestLanguage.Rule);

        outer.SetChild("extends", inner);

        var tree = new EditorTreeBuilder(TwoLineRuleNotation).Build(outer);
        var layout = Layouter.Layout(tree);
        var line = tree.Root.Children[0];

        // The inner rule is two lines tall, so the outer line is two rows tall and its closing brace
        // sits next to the inner rule's last line instead of below the whole child.
        AssertRect(0, 0, 21, 2, Bounds(layout, line));
        AssertRect(5, 0, 14, 2, Bounds(layout, line.Children[2]));
        AssertRect(20, 1, 1, 1, Bounds(layout, line.Children[4]));
        AssertRect(0, 2, 3, 1, Bounds(layout, layout.Tree.Root.Children[1]));
    }

    [Fact]
    public void AnUnsetPropertyStillOccupiesOneColumnSoTheCaretHasSomewhereToStand()
    {
        var step = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        var layout = Layouter.Layout(Builder.Build(step));
        var line = layout.Tree.Root;

        var index = Assert.IsType<PropertyCell>(line.Children[0]);
        Assert.False(index.IsSet);
        Assert.Equal(string.Empty, index.Text);

        AssertRect(0, 0, 1, 1, Bounds(layout, index));
        AssertRect(2, 0, 1, 1, Bounds(layout, line.Children[2]));
    }

    [Fact]
    public void AnEmptySlotShowsItsEmptyTextAndTakesTheRoomThatTextNeeds()
    {
        var step = TestLanguage.NewModel().NewNode(TestLanguage.Step);

        var layout = Layouter.Layout(Builder.Build(step));
        var detail = Assert.IsType<ChildCell>(layout.Tree.Root.Children[6]);

        Assert.True(detail.IsEmpty);
        AssertRect(6, 0, 7, 1, Bounds(layout, detail));
        Assert.Equal(13, layout.Size.Width);
    }

    [Fact]
    public void ACollectionLaysItsItemsAndSeparatorsOutOnOneLine()
    {
        var model = TestLanguage.NewModel();
        var body = model.NewNode(TestLanguage.Body);

        var first = model.NewChild(body, "notes", TestLanguage.Note);
        first.Set("text", "a");

        var second = model.NewChild(body, "notes", TestLanguage.Note);
        second.Set("text", "b");

        var layout = Layouter.Layout(Builder.Build(body));
        var notes = Assert.IsType<CollectionCell>(layout.Tree.Root.Children[1]);

        // note ␣ a ; ␣ note ␣ b
        AssertRect(0, 1, 6, 1, Bounds(layout, notes.Items[0]));
        AssertRect(6, 1, 1, 1, Bounds(layout, notes.Separators[0]));
        AssertRect(7, 1, 1, 1, Bounds(layout, notes.Content[2]));
        AssertRect(8, 1, 6, 1, Bounds(layout, notes.Items[1]));
        AssertRect(0, 1, 14, 1, Bounds(layout, notes));
    }

    [Fact]
    public void AnEmptyCollectionShowsItsEmptyText()
    {
        var body = TestLanguage.NewModel().NewNode(TestLanguage.Body);

        var layout = Layouter.Layout(Builder.Build(body));
        var notes = Assert.IsType<CollectionCell>(layout.Tree.Root.Children[1]);

        Assert.True(notes.IsEmpty);
        AssertRect(0, 1, 7, 1, Bounds(layout, notes));
    }

    [Fact]
    public void AConceptWithoutNotationIsLaidOutAsItsMarkerText()
    {
        var tag = TestLanguage.NewModel().NewNode(TestLanguage.Tag);

        var layout = Layouter.Layout(Builder.Build(tag));
        var marker = Assert.IsType<MissingNotationCell>(layout.Tree.Root);

        Assert.Equal($"<no notation for {TestLanguage.Tag}>", marker.Text);
        Assert.Equal(marker.Text.Length, layout.Size.Width);
        Assert.Equal(1, layout.Size.Height);
    }

    [Fact]
    public void EveryTextBearingCellAnswersWithTheTextItShows()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var tree = Builder.Build(rule);
        var line = Assert.IsType<ContainerCell>(tree.Root.Children[0]);

        Assert.Equal(
            ["rule", " ", "unnamed", " ", "as", " ", "<none>", " ", "{"],
            line.Content.Cast<TextCell>().Select(cell => cell.Text));
    }

    [Fact]
    public void TheBoundsOfANodeAddressAreTheOutermostCellCarryingIt()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var layout = Layouter.Layout(Builder.Build(rule));

        AssertRect(0, 0, 24, 4, layout.BoundsOf(new CellAddress(rule.Id)).GetValueOrDefault());
    }

    [Fact]
    public void ASlotAddressFindsTheCellShowingThatSlot()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var layout = Layouter.Layout(Builder.Build(rule));

        AssertRect(5, 0, 7, 1, layout.BoundsOf(CellAddress.OfSlot(rule.Id, "name")).GetValueOrDefault());
    }

    [Fact]
    public void HitTestingReturnsTheInnermostCellUnderThePoint()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var layout = Layouter.Layout(Builder.Build(rule));

        var hit = Assert.NotNull(layout.Find(6, 0));
        Assert.Equal("unnamed", Assert.IsType<PropertyCell>(hit.Cell).Text);
    }

    [Fact]
    public void HitTestingOutsideTheTreeFindsNothing()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);

        var layout = Layouter.Layout(Builder.Build(rule));

        Assert.Null(layout.Find(-1, 0));
        Assert.Null(layout.Find(0, 4));
        Assert.Null(layout.Find(24, 0));
    }

    [Fact]
    public void ACellFromAnotherTreeHasNoBounds()
    {
        var model = TestLanguage.NewModel();
        var rule = model.NewNode(TestLanguage.Rule);
        var other = model.NewNode(TestLanguage.Rule);

        var layout = Layouter.Layout(Builder.Build(rule));

        Assert.Null(layout.BoundsOf(Builder.Build(other).Root));
    }

    [Fact]
    public void LayingOutTheSameTreeTwiceGivesTheSamePositions()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var tree = Builder.Build(rule);

        var first = Layouter.Layout(tree);
        var second = Layouter.Layout(tree);

        Assert.Equal(first.Size, second.Size);

        for (var i = 0; i < first.Cells.Count; i++)
        {
            Assert.Same(first.Cells[i].Cell, second.Cells[i].Cell);
            Assert.Equal(first.Cells[i].Bounds, second.Cells[i].Bounds);
        }
    }

    [Fact]
    public void PlacementsAreInDocumentOrderWithContainersBeforeTheirContent()
    {
        var rule = TestLanguage.NewModel().NewNode(TestLanguage.Rule);
        var tree = Builder.Build(rule);

        var layout = Layouter.Layout(tree);

        Assert.Equal(tree.Cells().ToArray(), layout.Cells.Select(placed => placed.Cell).ToArray());
    }

    /// <summary>
    /// A rule whose notation has two lines, so that a rule nested inside a line makes that line tall.
    /// </summary>
    private static readonly NotationRegistry TwoLineRuleNotation = CreateTwoLineRuleNotation();

    private static NotationRegistry CreateTwoLineRuleNotation()
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        notations.Add(TestLanguage.Rule, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("rule"),
                ed.Space,
                ed.ChildInline("extends"),
                ed.Space,
                ed.Punct("}")),
            ed.Line(ed.Punct("end"))));

        notations.Add(TestLanguage.Body, ed => ed.Sequence(ed.Children("steps")));
        notations.Add(TestLanguage.Step, ed => ed.Line(ed.Property("text")));
        notations.Add(TestLanguage.Note, ed => ed.Line(ed.Property("text")));

        return notations;
    }

    [Fact]
    public void AStackedChildSlotStartsEveryChildOnItsOwnLine()
    {
        var stacked = ChildrenNotation(stacked: true);
        var inline = ChildrenNotation(stacked: false);

        var stackedLayout = Layouter.Layout(new EditorTreeBuilder(stacked).Build(Body(out var body)));

        var first = stackedLayout.BoundsOf(CellAddress.OfElement(body.Id, "steps", 0));
        var second = stackedLayout.BoundsOf(CellAddress.OfElement(body.Id, "steps", 1));

        Assert.NotNull(first);
        Assert.NotNull(second);

        // Same column, one line apart: the whole difference between a list and a column of children.
        Assert.Equal(first.Value.X, second.Value.X);
        Assert.Equal(first.Value.Y + MonospaceTextMeasure.Instance.LineHeight, second.Value.Y);

        var inlineLayout = Layouter.Layout(new EditorTreeBuilder(inline).Build(Body(out var sideBySide)));

        var left = inlineLayout.BoundsOf(CellAddress.OfElement(sideBySide.Id, "steps", 0));
        var right = inlineLayout.BoundsOf(CellAddress.OfElement(sideBySide.Id, "steps", 1));

        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.Equal(left.Value.Y, right.Value.Y);
        Assert.True(right.Value.X > left.Value.X);
    }

    /// <summary>A body holding two one-letter steps, which is all a list needs to show its direction.</summary>
    private static Node Body(out Node body)
    {
        var model = TestLanguage.NewModel();
        body = model.NewNode(TestLanguage.Body);
        model.AddRoot(body);

        foreach (var text in (string[])["a", "b"])
        {
            var step = model.NewChild(body, "steps", TestLanguage.Step);
            step.Set("text", text);
        }

        return body;
    }

    /// <summary>The test language's body, with its steps on one line or one per line.</summary>
    private static NotationRegistry ChildrenNotation(bool stacked)
    {
        var notations = new NotationRegistry(TestLanguage.Registry);

        notations.Add(TestLanguage.Body, ed => ed.Sequence(ed.Children("steps", stacked: stacked)));
        notations.Add(TestLanguage.Step, ed => ed.Line(ed.Property("text")));

        return notations;
    }

    private static LayoutRect Bounds(CellLayout layout, EditorCell cell)
    {
        var bounds = layout.BoundsOf(cell);

        Assert.True(bounds.HasValue, $"'{cell}' was not placed by the layout.");
        return bounds.GetValueOrDefault();
    }

    private static void AssertRect(double x, double y, double width, double height, LayoutRect actual) =>
        Assert.Equal(new LayoutRect(x, y, width, height), actual);
}
