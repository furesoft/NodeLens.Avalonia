using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The tooltip over a property that is still empty: the sentence the language declared on that slot.
/// </summary>
/// <remarks>
/// The condition is the whole feature. A property is the one place where "nothing entered yet" is a visible state
/// - the notation draws the cell even when it holds no text - so the box appears exactly there and disappears as
/// soon as there is something to read instead.
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
}
