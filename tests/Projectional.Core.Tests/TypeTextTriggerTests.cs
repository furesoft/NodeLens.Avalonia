using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// Typing an operator next to an expression that already exists, which is the half of text entry the typing
/// rules cannot answer.
/// </summary>
/// <remarks>
/// <para>
/// The fixture is <c>((5))</c>: a literal inside a group inside a group, so one document covers the three
/// shapes an edge can have - a property's offset, a node drawn as several cells, and a node that is the
/// document root and therefore has nowhere to put a wrapper.
/// </para>
/// <para>
/// Every test goes through <see cref="KeyRouter.Type(string)"/>, the same call the control makes, so what is
/// asserted is what a keystroke does rather than what a helper can be made to do.
/// </para>
/// </remarks>
public sealed class TypeTextTriggerTests
{
    [Fact]
    public void AnOperatorTypedAfterANumberWrapsItIntoABinary()
    {
        var (outer, group, literal) = Grouped();
        using var session = Open(outer);

        CaretAtEnd(session, literal, "value");

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("+")).Outcome);

        var binary = Assert.Single(group.GetChildren("inner"));

        Assert.Equal(ExpressionsConceptKeys.Binary, binary.Concept.Key);
        Assert.Equal("+", binary.GetOrNull("operator"));
        Assert.Same(literal, binary.GetChild("left"));
        Assert.Empty(binary.GetChildren("right"));

        // The caret continues in the operand the user has not written yet, not inside the one they just left.
        Assert.Equal(CellAddress.OfSlot(binary.Id, "right"), session.Selection.Caret.Address);
    }

    [Fact]
    public void TheNextKeystrokeFillsTheOperandTheOperatorOpened()
    {
        var (outer, group, literal) = Grouped();
        using var session = Open(outer);

        CaretAtEnd(session, literal, "value");
        session.Apply(KeyRouter.Type("+"));

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("3")).Outcome);

        var binary = Assert.Single(group.GetChildren("inner"));
        var right = Assert.Single(binary.GetChildren("right"));

        Assert.Equal(ExpressionsConceptKeys.IntLiteral, right.Concept.Key);
        Assert.Equal("3", right.GetOrNull("value"));
    }

    [Fact]
    public void ACharacterThePropertyAcceptsIsNeverTakenByATrigger()
    {
        // A minus in front of a number makes a negative number, which is still a number: the property wins,
        // and the negation trigger is never asked. That is the rule that keeps triggers from stealing text.
        var (outer, group, literal) = Grouped();
        using var session = Open(outer);

        CaretAtStart(session, literal, "value");

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("-")).Outcome);
        Assert.Equal("-5", literal.GetOrNull("value"));
        Assert.Same(literal, Assert.Single(group.GetChildren("inner")));
    }

    [Fact]
    public void AnOperatorTypedBeforeATruthValueNegatesIt()
    {
        // A boolean takes no minus, so the keystroke reaches the trigger - and the negation is declared
        // before the subtraction, so it is the one that answers.
        var (outer, group, boolean) = Grouped(ExpressionsConceptKeys.BoolLiteral, "true");
        using var session = Open(outer);

        CaretAtStart(session, boolean, "value");

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("!")).Outcome);

        var unary = Assert.Single(group.GetChildren("inner"));

        Assert.Equal(ExpressionsConceptKeys.Unary, unary.Concept.Key);
        Assert.Equal("!", unary.GetOrNull("operator"));
        Assert.Same(boolean, unary.GetChild("operand"));

        // A prefix operator does not move the user away from what they were writing.
        Assert.Equal(CellAddress.OfSlot(boolean.Id, "value"), session.Selection.Caret.Address);
    }

    [Fact]
    public void AnOperatorTypedAfterAGroupExtendsIt()
    {
        // The caret is on the closing parenthesis, which is the last cell the group owns.
        var (outer, group, _) = Grouped();
        using var session = Open(outer);

        CaretOn(session, group, 1);

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("*")).Outcome);

        var binary = Assert.Single(outer.GetChildren("inner"));

        Assert.Equal(ExpressionsConceptKeys.Binary, binary.Concept.Key);
        Assert.Equal("*", binary.GetOrNull("operator"));
        Assert.Same(group, binary.GetChild("left"));
        Assert.Equal(CellAddress.OfSlot(binary.Id, "right"), session.Selection.Caret.Address);
    }

    [Fact]
    public void AnOperatorTypedBeforeAGroupGoesInFrontOfIt()
    {
        // The opening parenthesis is the first cell the group owns, so the existing expression becomes the
        // right operand and the caret waits in the left one.
        var (outer, group, _) = Grouped();
        using var session = Open(outer);

        CaretOn(session, group, 0);

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("+")).Outcome);

        var binary = Assert.Single(outer.GetChildren("inner"));

        Assert.Equal(ExpressionsConceptKeys.Binary, binary.Concept.Key);
        Assert.Same(group, binary.GetChild("right"));
        Assert.Empty(binary.GetChildren("left"));
        Assert.Equal(CellAddress.OfSlot(binary.Id, "left"), session.Selection.Caret.Address);
    }

    [Fact]
    public void ARootIsNeverWrapped()
    {
        // The outermost group has no parent, so there is no slot a wrapper could stand in. A keystroke has to
        // survive that, which is why the action asks before it wraps instead of letting the model refuse.
        var (outer, group, _) = Grouped();
        using var session = Open(outer);

        CaretOn(session, outer, 1);

        Assert.Equal(EditOutcome.Unchanged, session.Apply(KeyRouter.Type("+")).Outcome);

        // The document is still one group holding one group: nothing was wrapped around the root.
        Assert.Same(group, Assert.Single(outer.GetChildren("inner")));
    }

    [Fact]
    public void ACaretInsideAValueIsNotAnEdge()
    {
        // A caret in the middle of "51" is not "next to" anything: an operator there would have to decide
        // which half of the number it belongs to.
        var (outer, group, literal) = Grouped(ExpressionsConceptKeys.IntLiteral, "51");
        using var session = Open(outer);

        var place = Place(session, literal, "value");
        session.Select(Selection.At(CaretPosition.At(place, 1)));

        Assert.Equal(EditOutcome.Rejected, session.Apply(KeyRouter.Type("+")).Outcome);
        Assert.Same(literal, Assert.Single(group.GetChildren("inner")));
        Assert.Equal("51", literal.GetOrNull("value"));
    }

    [Fact]
    public void ASelectionIsTypedOverRatherThanTurnedIntoAWrap()
    {
        // Replacing selected text is what a selection is for; wrapping it is what Ctrl+A twice and an
        // intention are for.
        var (outer, group, literal) = Grouped();
        using var session = Open(outer);

        var place = Place(session, literal, "value");
        session.Select(new Selection(CaretPosition.At(place, 0), CaretPosition.At(place, place.Length)));

        Assert.Equal(EditOutcome.Rejected, session.Apply(KeyRouter.Type("+")).Outcome);
        Assert.Same(literal, Assert.Single(group.GetChildren("inner")));
    }

    [Fact]
    public void AnEmptySlotAsksTheTypingRulesInstead()
    {
        // An operator typed into an empty slot is not next to anything, and no typing rule claims it.
        var model = ExpressionsLanguage.NewProject().NewModel();
        var group = model.NewNode(ExpressionsConceptKeys.Parenthesized);

        model.AddRoot(group);

        using var session = Open(group);

        session.Select(Selection.At(CellAddress.OfSlot(group.Id, "inner")));

        Assert.Equal(EditOutcome.Unchanged, session.Apply(KeyRouter.Type("+")).Outcome);
        Assert.Empty(group.GetChildren("inner"));
    }

    [Fact]
    public void TheWrapIsItsOwnUndoStep()
    {
        // Typing "1" and then "+" are two gestures: two presses of undo must give back "51" and then "5",
        // not take the number away with the operator it produced.
        var (outer, group, literal) = Grouped();
        using var session = Open(outer);

        CaretAtEnd(session, literal, "value");
        session.Apply(KeyRouter.Type("1"));
        session.Apply(KeyRouter.Type("+"));

        Assert.Equal(2, session.History.UndoDepth);
        Assert.Equal(ExpressionsConceptKeys.Binary, Assert.Single(group.GetChildren("inner")).Concept.Key);

        Assert.True(session.Undo());

        var restored = Assert.Single(group.GetChildren("inner"));

        Assert.Same(literal, restored);
        Assert.Equal("51", restored.GetOrNull("value"));

        Assert.True(session.Undo());
        Assert.Equal("5", literal.GetOrNull("value"));
    }

    [Fact]
    public void ALanguageWithoutTriggersRejectsTheOperator()
    {
        // No triggers is the state every language was in before this existed, and it still has to be the
        // behaviour: a refused character stays refused.
        var (outer, group, literal) = Grouped();
        using var session = new EditorSession(
            outer,
            Notations,
            typing: ExpressionsLanguage.TypingRules);

        CaretAtEnd(session, literal, "value");

        Assert.Equal(EditOutcome.Rejected, session.Apply(KeyRouter.Type("+")).Outcome);
        Assert.Same(literal, Assert.Single(group.GetChildren("inner")));
    }

    [Fact]
    public void ATriggerWithoutAPropertySlotOnlyWraps()
    {
        // A gesture that only means "put this concept around it": nothing is written, and the caret stays
        // inside the node that was wrapped.
        var (outer, group, literal) = Grouped();
        using var session = new EditorSession(
            outer,
            Notations,
            triggers: TriggerRegistry.Empty.Add(new ConceptTrigger(
                ExpressionsConceptKeys.Parenthesized,
                ExpressionsConceptKeys.Expression,
                TriggerSide.Right,
                text => text == "(",
                anchorSlot: "inner")));

        CaretAtEnd(session, literal, "value");

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("(")).Outcome);

        var wrapper = Assert.Single(group.GetChildren("inner"));

        Assert.Equal(ExpressionsConceptKeys.Parenthesized, wrapper.Concept.Key);
        Assert.Same(literal, wrapper.GetChild("inner"));
        Assert.Equal(CellAddress.OfSlot(literal.Id, "value"), session.Selection.Caret.Address);
    }

    [Fact]
    public void ATriggerWhosePropertyRefusesTheTextStillWraps()
    {
        // A declared trigger that claims more than its wrapper can store is a language mistake, and a
        // keystroke must not be able to throw over it: the concept appears, the text is dropped.
        var (outer, group, literal) = Grouped();
        using var session = new EditorSession(
            outer,
            Notations,
            triggers: TriggerRegistry.Empty.Add(new ConceptTrigger(
                ExpressionsConceptKeys.Binary,
                ExpressionsConceptKeys.Expression,
                TriggerSide.Right,
                text => text == "??",
                anchorSlot: "left",
                propertySlot: "operator",
                caretSlot: "right")));

        CaretAtEnd(session, literal, "value");

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("??")).Outcome);

        var binary = Assert.Single(group.GetChildren("inner"));

        Assert.Equal(ExpressionsConceptKeys.Binary, binary.Concept.Key);
        Assert.Null(binary.GetOrNull("operator"));
        Assert.Same(literal, binary.GetChild("left"));
    }

    [Fact]
    public void ATriggerForAConceptTheLanguageDoesNotHaveDoesNothing()
    {
        var (outer, group, literal) = Grouped();
        using var session = new EditorSession(
            outer,
            Notations,
            triggers: TriggerRegistry.Empty.Add(new ConceptTrigger(
                "Expressions.NoSuchConcept",
                ExpressionsConceptKeys.Expression,
                TriggerSide.Right,
                text => text == "+",
                anchorSlot: "left")));

        CaretAtEnd(session, literal, "value");

        Assert.Equal(EditOutcome.Rejected, session.Apply(KeyRouter.Type("+")).Outcome);
        Assert.Same(literal, Assert.Single(group.GetChildren("inner")));
    }

    [Fact]
    public void TheFirstDeclaredTriggerWins()
    {
        var (outer, group, boolean) = Grouped(ExpressionsConceptKeys.BoolLiteral, "true");
        using var session = new EditorSession(
            outer,
            Notations,
            triggers: TriggerRegistry.Empty
                .Add(new ConceptTrigger(
                    ExpressionsConceptKeys.Unary,
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Left,
                    text => text == "-",
                    anchorSlot: "operand",
                    propertySlot: "operator"))
                .Add(new ConceptTrigger(
                    ExpressionsConceptKeys.Binary,
                    ExpressionsConceptKeys.Expression,
                    TriggerSide.Left,
                    text => text == "-",
                    anchorSlot: "right",
                    propertySlot: "operator",
                    caretSlot: "left")));

        CaretAtStart(session, boolean, "value");
        session.Apply(KeyRouter.Type("-"));

        Assert.Equal(ExpressionsConceptKeys.Unary, Assert.Single(group.GetChildren("inner")).Concept.Key);
    }

    [Fact]
    public void AnOpeningParenthesisTypedBeforeAnExpressionGroupsIt()
    {
        // A parenthesis is a gesture rather than a value: nothing is written, and the caret stays inside the
        // expression that was grouped.
        var (outer, group, literal) = Grouped();
        using var session = Open(outer);

        CaretAtStart(session, literal, "value");

        Assert.Equal(EditOutcome.Applied, session.Apply(KeyRouter.Type("(")).Outcome);

        var wrapper = Assert.Single(group.GetChildren("inner"));

        Assert.Equal(ExpressionsConceptKeys.Parenthesized, wrapper.Concept.Key);
        Assert.Same(literal, wrapper.GetChild("inner"));
        Assert.Equal(CellAddress.OfSlot(literal.Id, "value"), session.Selection.Caret.Address);
    }

    [Fact]
    public void TheShippedLanguagesDeclareTriggersBesideTheirTypingRules()
    {
        Assert.False(ExpressionsLanguage.Triggers.IsEmpty);
        Assert.Same(ExpressionsLanguage.Triggers, RulesLanguage.Triggers);
    }

    [Fact]
    public void TheOperatorBecomesPartOfTheDrawnDocument()
    {
        // The projection is what the user reads, so the proof is a drawing: the five, the operator that
        // created the addition, and the empty operand the caret is waiting in.
        var (outer, _, literal) = Grouped();
        using var session = Open(outer);

        CaretAtEnd(session, literal, "value");
        session.Apply(KeyRouter.Type("+"));

        Assert.Equal("((5 + <empty>))", LayoutSnapshot.Render(session.Layout, width: 40));
    }

    private static NotationRegistry Notations => ExpressionsLanguage.CreateNotations(ExpressionsLanguage.Concepts);

    /// <summary>A literal inside a group inside the document's group, which is the root.</summary>
    private static (Node Outer, Node Group, Node Literal) Grouped(
        string literalConcept = ExpressionsConceptKeys.IntLiteral,
        string value = "5")
    {
        var model = ExpressionsLanguage.NewProject().NewModel();
        var outer = model.NewNode(ExpressionsConceptKeys.Parenthesized);

        model.AddRoot(outer);

        var group = model.NewChild(outer, "inner", ExpressionsConceptKeys.Parenthesized);
        var literal = model.NewChild(group, "inner", literalConcept);

        literal.Set("value", value);

        return (outer, group, literal);
    }

    private static EditorSession Open(Node document) =>
        new(
            document,
            Notations,
            typing: ExpressionsLanguage.TypingRules,
            triggers: ExpressionsLanguage.Triggers);

    private static void CaretAtStart(EditorSession session, Node node, string slot)
    {
        var place = Place(session, node, slot);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    private static void CaretAtEnd(EditorSession session, Node node, string slot)
    {
        var place = Place(session, node, slot);

        session.Select(Selection.At(CaretPosition.At(place, place.Length)));
    }

    /// <summary>Puts the caret on the <paramref name="index"/>-th cell <paramref name="node"/> owns.</summary>
    /// <remarks>
    /// A node drawn as keywords and punctuation owns several cells that share no address with any property, so
    /// the only way to say "the caret is on the closing parenthesis" is to walk the places in document order.
    /// </remarks>
    private static void CaretOn(EditorSession session, Node node, int index)
    {
        var place = CaretPlaces.Of(session.Tree).Where(each => each.Address.Owner == node.Id).ElementAt(index);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }

    private static CaretPlace Place(EditorSession session, Node node, string slot) =>
        CaretPlaces.Of(session.Tree).First(each => each.Address.Equals(CellAddress.OfSlot(node.Id, slot)));
}
