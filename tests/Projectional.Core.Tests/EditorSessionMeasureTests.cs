using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Changing the measure a session lays its document out with - what a font size change does to the
/// projection.
/// </summary>
/// <remarks>
/// The rule these tests pin is that the measure is view state: swapping it re-lays-out the document,
/// keeps the caret somewhere that exists, and leaves the model and the undo history alone. A host that
/// gets that wrong shows a document whose cells are in the wrong place while claiming it was never
/// edited.
/// </remarks>
public sealed class EditorSessionMeasureTests
{
    [Fact]
    public void ASessionMeasuresWithWhatItWasGiven()
    {
        var measure = new ScaledTextMeasure(2);

        using var session = new EditorSession(TestEditor.BareRule(), TestNotations.Registry, measure);

        Assert.Same(measure, session.Measure);
    }

    [Fact]
    public void ASessionMeasuresInMonospaceWhenGivenNothing()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Same(MonospaceTextMeasure.Instance, session.Measure);
    }

    [Fact]
    public void ChangingTheMeasureRelaysOutTheDocument()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());
        var before = session.Layout.Size;
        var tree = session.Tree;

        session.Measure = new ScaledTextMeasure(3);

        Assert.NotSame(tree, session.Tree);
        Assert.Equal(before.Width * 3, session.Layout.Size.Width, 6);
        Assert.Equal(before.Height * 3, session.Layout.Size.Height, 6);
    }

    [Fact]
    public void ChangingTheMeasureKeepsTheCaretOnACellOfTheNewTree()
    {
        var (_, _, step) = TestEditor.RuleWithStep();
        using var session = TestEditor.Open(step);
        var caret = session.Selection.Caret;

        session.Measure = new ScaledTextMeasure(2);

        Assert.Equal(caret, session.Selection.Caret);
        Assert.NotNull(CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret));
    }

    [Fact]
    public void ChangingTheMeasureKeepsTheWrapWidth()
    {
        using var session = TestEditor.Open(TestEditor.RuleWithStep().Rule);
        session.WrapWidth = 6;
        var height = session.Layout.Size.Height;

        session.Measure = new ScaledTextMeasure(2);

        Assert.Equal<double?>(6, session.WrapWidth);
        Assert.True(
            session.Layout.Size.Height > height,
            $"the same width in a wider measure must wrap more, but the layout stayed {height} tall");
    }

    [Fact]
    public void ChangingTheMeasureIsNotAnEdit()
    {
        using var session = TestEditor.Open(TestEditor.RuleWithStep().Rule);

        session.Measure = new ScaledTextMeasure(2);

        Assert.False(session.History.CanUndo);
        Assert.Equal(0, session.History.UndoDepth);
    }

    [Fact]
    public void SettingTheSameMeasureRebuildsNothing()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());
        var measure = session.Measure;
        var tree = session.Tree;
        var layout = session.Layout;

        session.Measure = measure;

        Assert.Same(tree, session.Tree);
        Assert.Same(layout, session.Layout);
    }

    [Fact]
    public void TheMeasureCannotBeNull()
    {
        using var session = TestEditor.Open(TestEditor.BareRule());

        Assert.Throws<ArgumentNullException>(() => session.Measure = null!);
    }
}
