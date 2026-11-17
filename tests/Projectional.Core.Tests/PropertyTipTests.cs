using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The tooltip over a property: the sentence the language declared on that slot.
/// </summary>
/// <remarks>
/// The condition is the whole feature. A property is the one place where "nothing entered yet" is a visible state
/// - the notation draws the cell even when it holds no text - so the box appears there without being asked for,
/// and over a value only when the slot itself says its text does not explain it.
/// </remarks>
public sealed class PropertyTipTests
{
    private const string Sentence = "What the step does.";

    [Fact]
    public void AnEmptyPropertyWithADescriptionHasATip()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step.Model.Roots[0]);

        var content = PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(step.Id, "text")));

        Assert.NotNull(content);
        Assert.Equal("text", content.Heading);
        Assert.Equal(Sentence, content.Description);
    }

    [Fact]
    public void APropertyThatAlreadyHoldsTextHasNoTip()
    {
        // Once there is a value, the value is the answer - a box over the text would be in the way of reading it.
        var step = TestEditor.BareStep("say hi");
        using var session = TestEditor.Open(step.Model.Roots[0]);

        Assert.Null(PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(step.Id, "text"))));
    }

    [Fact]
    public void APropertyThatAsksForItsTipOverTheValueShowsIt()
    {
        // A language opts in per slot, for values whose text does not explain itself. A note's prose is exactly
        // that case: the sentence says what belongs in the cell, which reading "remember the milk" does not.
        var note = Note("remember the milk");
        using var session = TestEditor.Open(note);

        var content = PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(note.Id, "text")));

        Assert.NotNull(content);
        Assert.Equal("text", content.Heading);
        Assert.Equal("What the note says.", content.Description);
    }

    [Fact]
    public void ASlotThatAsksForItsTipOverTheValueShowsItWhileEmptyToo()
    {
        // The flag adds the filled case; it does not replace the empty one, which is the state the feature began
        // with. A box that only appeared after typing would be missing exactly when the question is asked.
        var note = Note();
        using var session = TestEditor.Open(note);

        Assert.NotNull(PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(note.Id, "text"))));
    }

    [Fact]
    public void AnOptInWithoutADescriptionIsRefusedWhenTheLanguageDeclaresIt()
    {
        // The box would show nothing, ever. A language that asks for it has made a mistake, and the declaration
        // is the earliest moment the mistake can be named.
        Assert.Throws<ArgumentException>(() => new PropertySlot(
            "text",
            PropertyType.String,
            showDescriptionWhenFilled: true));
    }

    [Fact]
    public void APropertyWithoutADescriptionHasNoTip()
    {
        // The same rule the concept tip follows: no sentence, no box. An empty box teaches the user that
        // hovering is pointless.
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step.Model.Roots[0]);

        Assert.Null(PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(step.Id, "index"))));
    }

    [Fact]
    public void AChildSlotHasNoTip()
    {
        // A child slot is a place a node stands, not a box a value goes into; its documentation is the child
        // concept's, which the concept tip already shows.
        var (rule, _) = TestEditor.RuleWithBody();
        using var session = TestEditor.Open(rule.Model.Roots[0]);

        Assert.Null(PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(rule.Id, "body"))));
    }

    [Fact]
    public void ANodeThatNoLongerExistsHasNoTip()
    {
        // A pointer can outlive its node, and a tooltip must never throw at the moment the user moves the mouse.
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step.Model.Roots[0]);
        var address = CellAddress.OfSlot(step.Id, "text");

        step.Model.DeleteSubtree(step, record: false);

        Assert.Null(PropertyTip.Of(session, CaretPosition.At(address)));
    }

    [Fact]
    public void TheSlotNameIsTheHeadingAndTheLinesPutItFirst()
    {
        var step = TestEditor.BareStep();
        using var session = TestEditor.Open(step.Model.Roots[0]);

        var content = PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(step.Id, "text")))!;
        var lines = TipLines.Of(content, 80, MonospaceTextMeasure.Instance);

        Assert.Equal("text", lines[0]);
        Assert.Equal(Sentence, string.Join(' ', lines.Skip(1)));
    }

    [Fact]
    public void ADefaultedPropertyIsNotTreatedAsEmpty()
    {
        // The model cannot tell a default from a value the user typed, so a slot born with one reads as filled.
        // Documented rather than pretended: the tip is about "nothing entered yet", and a default is something.
        var rule = TestEditor.BareRule();
        using var session = TestEditor.Open(rule.Model.Roots[0]);

        Assert.Equal("unnamed", rule.GetOrNull("name"));
        Assert.Null(PropertyTip.Of(session, CaretPosition.At(CellAddress.OfSlot(rule.Id, "name"))));
    }

    [Fact]
    public void ABlankDescriptionIsRefusedWhenTheLanguageDeclaresIt()
    {
        Assert.Throws<ArgumentException>(() => new PropertySlot("text", PropertyType.String, description: " "));
    }

    private static Node Note(string? text = null)
    {
        var note = TestLanguage.NewModel().NewNode(TestLanguage.Note);

        if (text is not null)
        {
            note.Set("text", text);
        }

        return note;
    }
}
