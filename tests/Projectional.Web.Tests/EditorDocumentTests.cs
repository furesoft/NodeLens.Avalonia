using Projectional.Editing;
using Projectional.Projection;
using Xunit;

namespace Projectional.Web.Tests;

public sealed class EditorDocumentTests
{
    [Fact]
    public void TypingIntoAValueChangesTheModelAndMakesTheDocumentDirty()
    {
        var language = WebLanguages.Rules;
        using var document = new EditorDocument("id", "payroll", language, language.NewSample());

        var ruleset = document.Model.Roots[0];
        document.Session.Select(Selection.At(CellAddress.OfSlot(ruleset.Id, "name")));
        document.Type("X");

        Assert.Equal("XPayroll", ruleset.GetOrNull("name"));
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void AcceptingTheProposalThatIsAlreadyHighlightedDoesNotThrow()
    {
        var language = WebLanguages.StateMachine;
        using var document = new EditorDocument("id", "machine", language, language.NewSample());

        // The ship transition is the one the sample leaves without a guard, so its slot has proposals.
        var transition = document.Model.Nodes.First(node =>
            node.Concept.Key == "Sm.Transition" && node.GetOrNull("event") == "ship");

        document.Session.Select(Selection.At(CellAddress.OfSlot(transition.Id, "guard")));
        document.Press(EditorKey.ControlSpace);

        Assert.NotNull(document.Session.Completion);

        // The first row is highlighted when the list opens; clicking it is a zero move, which used to throw.
        document.AcceptCompletion(0);

        Assert.Null(document.Session.Completion);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void UndoRestoresTheValueAndTheSavedState()
    {
        var language = WebLanguages.Rules;
        using var document = new EditorDocument("id", "payroll", language, language.NewSample());

        var ruleset = document.Model.Roots[0];
        document.Session.Select(Selection.At(CellAddress.OfSlot(ruleset.Id, "name")));
        document.Type("X");

        Assert.True(document.IsDirty);

        Assert.True(document.Undo());
        Assert.Equal("Payroll", ruleset.GetOrNull("name"));
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void AWordSelectionIsAValueSelection()
    {
        var language = WebLanguages.Rules;
        using var document = new EditorDocument("id", "payroll", language, language.NewSample());

        var ruleset = document.Model.Roots[0];
        var address = CellAddress.OfSlot(ruleset.Id, "name");

        document.Session.Select(new Selection(CaretPosition.At(address), CaretPosition.At(address, 3)));
        document.SelectWord();

        Assert.True(document.Session.Selection.HasText);
        Assert.Equal(0, document.Session.Selection.Start);
        Assert.Equal("Payroll".Length, document.Session.Selection.End);
    }
}
