using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Typing into an empty slot is what makes a projectional language writable rather than only readable, so
/// these tests hold both halves of it: the language decides which concept a text belongs to, and the
/// action creates that concept, fills it in and leaves the caret where the next character goes.
/// </summary>
/// <remarks>
/// The document is a binary expression whose two operands are empty, because that is the case the user
/// reported: after inserting the operator there is nothing to type into, since the only property in reach
/// belongs to a node that does not exist yet.
/// </remarks>
public sealed class TypeTextActionTests
{
    private static NotationRegistry Notations => ExpressionsLanguage.CreateNotations(ExpressionsLanguage.Concepts);

    [Fact]
    public void ADigitInAnEmptySlotCreatesTheIntegerLiteral()
    {
        var binary = NewBinary();
        using var session = Open(binary);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        var result = session.Apply(KeyRouter.Type("4"));

        Assert.Equal(EditOutcome.Applied, result.Outcome);

        var created = Assert.Single(binary.GetChildren("left"));
        Assert.Equal(ExpressionsConceptKeys.IntLiteral, created.Concept.Key);
        Assert.Equal(4, created.GetInt("value"));

        // Behind the digit, so that a second one appends rather than pushing in front of it.
        Assert.Equal(CellAddress.OfSlot(created.Id, "value"), session.Selection.Caret.Address);
        Assert.Equal(1, session.Selection.Caret.Offset);
    }

    [Fact]
    public void AWordInAnEmptySlotCreatesTheName()
    {
        // The interesting half of the same question: a name and a string literal both hold "amount", and
        // the declaration order of the concepts decides that a bare word is the name.
        var binary = NewBinary();
        using var session = Open(binary);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "right")));
        session.Apply(KeyRouter.Type("amount"));

        var created = Assert.Single(binary.GetChildren("right"));
        Assert.Equal(ExpressionsConceptKeys.Name, created.Concept.Key);
        Assert.Equal("amount", created.GetOrNull("name"));
        Assert.Equal(6, session.Selection.Caret.Offset);
    }

    [Fact]
    public void AQuoteInAnEmptySlotCreatesTheStringLiteral()
    {
        var binary = NewBinary();
        using var session = Open(binary);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        session.Apply(KeyRouter.Type("\""));

        var created = Assert.Single(binary.GetChildren("left"));
        Assert.Equal(ExpressionsConceptKeys.StringLiteral, created.Concept.Key);
        Assert.Equal("\"", created.GetOrNull("value"));
    }

    [Fact]
    public void TheCharactersAfterTheFirstGoIntoThePropertyTheFirstOneCreated()
    {
        var binary = NewBinary();
        using var session = Open(binary);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        session.Apply(KeyRouter.Type("1"));
        session.Apply(KeyRouter.Type("2"));

        var created = Assert.Single(binary.GetChildren("left"));
        Assert.Equal(12, created.GetInt("value"));
        Assert.Equal(2, session.Selection.Caret.Offset);
    }

    [Fact]
    public void AConceptWithoutAPropertyIsCreatedAndTheCaretGoesInsideIt()
    {
        // Grouping has no property to write into, so the parenthesis is not text at all - it selects the
        // concept - and the caret has to end up where the grouped expression belongs, not after it.
        var binary = NewBinary();
        using var session = Open(binary);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        session.Apply(KeyRouter.Type("("));

        var group = Assert.Single(binary.GetChildren("left"));
        Assert.Equal(ExpressionsConceptKeys.Parenthesized, group.Concept.Key);
        Assert.Equal(CellAddress.OfSlot(group.Id, "inner"), session.Selection.Caret.Address);

        session.Apply(KeyRouter.Type("7"));

        Assert.Equal(ExpressionsConceptKeys.IntLiteral, Assert.Single(group.GetChildren("inner")).Concept.Key);
    }

    [Fact]
    public void TextNoConceptClaimsLeavesTheSlotEmpty()
    {
        var binary = NewBinary();
        using var session = Open(binary);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        var result = session.Apply(KeyRouter.Type("@"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Empty(binary.GetChildren("left"));
    }

    [Fact]
    public void ALanguageWithoutTypingRulesLeavesTheSlotEmpty()
    {
        // The rules are what enables this, not a guess inside the action: a language that declares none
        // keeps the behaviour every language had before they existed.
        var binary = NewBinary();
        using var session = new EditorSession(binary, Notations);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        var result = session.Apply(KeyRouter.Type("4"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Empty(binary.GetChildren("left"));
    }

    [Fact]
    public void CreatingANodeAndWritingItsTextIsOneUndoStep()
    {
        var binary = NewBinary();
        using var session = Open(binary);

        session.Select(Selection.At(CellAddress.OfSlot(binary.Id, "left")));
        session.Apply(KeyRouter.Type("4"));

        Assert.Equal(1, session.History.UndoDepth);

        Assert.True(session.Undo());
        Assert.Empty(binary.GetChildren("left"));
    }

    [Fact]
    public void ATypedSelectionInAnEmptySlotCreatesTheNodeInsteadOfTypingOverIt()
    {
        var binary = NewBinary();
        using var session = Open(binary);
        var left = CellAddress.OfSlot(binary.Id, "left");
        var right = CellAddress.OfSlot(binary.Id, "right");

        session.Select(new Selection(CaretPosition.At(left), CaretPosition.At(right)));
        var result = session.Apply(KeyRouter.Type("4"));

        Assert.Equal(EditOutcome.Unchanged, result.Outcome);
        Assert.Empty(binary.GetChildren("left"));
        Assert.Empty(binary.GetChildren("right"));
    }

    /// <summary>An expression whose operator is set and whose two operands are empty slots.</summary>
    private static Node NewBinary()
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);
        binary.Set("operator", "+");
        model.AddRoot(binary);

        return binary;
    }

    private static EditorSession Open(Node document) =>
        new(document, Notations, typing: ExpressionsLanguage.TypingRules);
}
