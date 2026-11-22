using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.IntentionRules;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// A language's own intentions, written as documents: the rule lowers into the same entry a hand-written package
/// registers, and applying it produces the same edit.
/// </summary>
/// <remarks>
/// The fixture is the negation, because it is exactly the entry a declaration cannot derive: a <c>Unary</c> born
/// with <c>operator = "!"</c>.
/// </remarks>
public sealed class IntentionLoweringTests
{
    [Fact]
    public void ADeclaredNegationIsOfferedWhereTheHandWrittenOneIs()
    {
        var (session, _, condition) = Document();

        Caret(session, condition);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Negate", offer.Label);
        Assert.Equal("intent.Expressions.Expression.Negate", offer.Id);
    }

    [Fact]
    public void ApplyingItWrapsTheNodeWithThePresetValue()
    {
        var (session, rule, condition) = Document();

        Caret(session, condition);

        session.Apply(IntentionList.For(session).Single());

        var unary = Assert.Single(rule.GetChildren("condition"));

        Assert.Equal(ExpressionsConceptKeys.Unary, unary.Concept.Key);
        Assert.Equal("!", unary.GetOrNull("operator"));
        Assert.Same(condition, unary.GetChild("operand"));
    }

    [Fact]
    public void ARuleOnAConceptIsNotOfferedElsewhere()
    {
        var (session, _, _) = Document();

        // The rule is about expressions; the ruleset's name is not one.
        session.Select(Selection.At(CellAddress.OfSlot(session.Document.Id, "name")));

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void AConceptThatIsNotDeclaredIsRefused()
    {
        // The rule names a concept of another language, which this registry cannot answer for.
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(RulesConceptKeys.Rule));

        Assert.Contains("not a concept", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APresetThatNamesNoPropertyOfTheWrapperIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(preset: ("nothing", "x")));

        Assert.Contains("no property named 'nothing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoIntentionsWithOneLabelOnOneConceptAreRefused()
    {
        var first = Rule().Rule;
        var second = Rule().Rule;

        var exception = Assert.Throws<ConceptDefinitionException>(
            () => IntentionLowering.CreateProvider([first, second], ExpressionsLanguage.Concepts));

        Assert.Contains("one entry per label", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredExtractRunsAsTheEditTheActionWould()
    {
        // The subject is the extract fixture's own shape: a step in a chain, a `library` on the container above it,
        // and a call that stands where a step stands and points back at one - the same three facts the workflow
        // sample states, in the smallest language that has them.
        var (container, steps) = ExtractLanguage.WithSteps("build");

        using var session = ExtractLanguage.Open(container);

        session.AddIntentions(
            IntentionLowering.CreateProvider([ExtractRule()], ExtractLanguage.Concepts),
            IntentionSource.Language);

        Caret(session, steps[0]);

        var offer = Assert.Single(IntentionList.For(session));

        Assert.Equal("Extract to the library", offer.Label);
        Assert.Equal(EditSafety.Plain, offer.Safety);
        Assert.Equal(EditOutcome.Applied, session.Apply(offer).Outcome);
        Assert.Same(steps[0], Assert.Single(container.GetChildren("library")));
        Assert.Equal(
            ExtractLanguage.Call,
            Assert.Single(container.GetChild("chain")!.GetChildren("steps")).Concept.Key);
    }

    [Fact]
    public void ADeclaredExtractIsNotOfferedOnANodeThatIsAlreadyInTheDestination()
    {
        // An offer that comes back rejected is worse than no offer, so the place is part of the declaration.
        var (container, _) = ExtractLanguage.WithSteps("build");
        var extracted = container.Model.NewChild(container, "library", ExtractLanguage.Step);

        using var session = ExtractLanguage.Open(container);

        session.AddIntentions(
            IntentionLowering.CreateProvider([ExtractRule()], ExtractLanguage.Concepts),
            IntentionSource.Language);

        Caret(session, extracted);

        Assert.Empty(IntentionList.For(session));
    }

    [Fact]
    public void AnExtractWithNoDestinationIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => LowerExtract(into: ""));

        Assert.Contains("has to name the concept that receives the node", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExtractThatNamesAConceptOfAnotherLanguageIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => LowerExtract(into: "Other.Rule"));

        Assert.Contains("not a concept", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExtractWhoseDestinationDoesNotHoldTheSubjectIsRefused()
    {
        // `chain` holds a run of steps, and the rule is about steps: the extract could never run, so it must not load.
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => LowerExtract(into: ExtractLanguage.Container, slot: "chain"));

        Assert.Contains("does not accept 'Extract.Step'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExtractWhoseUseCannotStandWhereTheSubjectStandsIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => LowerExtract(asConcept: ExtractLanguage.Chain));

        Assert.Contains("is not a 'Extract.Step'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExtractWhoseUseHasNoSuchReferenceIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => LowerExtract(reference: "nowhere"));

        Assert.Contains("declares no reference named 'nowhere'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExtractWhoseNamePropertyIsNotTextIsRefused()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => LowerExtract(name: "attempts"));

        Assert.Contains("writes text into it", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>An extract of any step into the container's library, leaving a call behind.</summary>
    private static Node ExtractRule(
        string conceptKey = ExtractLanguage.Step,
        string into = ExtractLanguage.Container,
        string slot = "library",
        string asConcept = ExtractLanguage.Call,
        string reference = "calls",
        string? name = null)
    {
        var model = IntentionLanguage.NewProject().NewModel("extract");
        var rule = model.NewNode(IntentionConceptKeys.Rule);

        rule.Set(IntentionLanguage.ConceptKeySlot, conceptKey);
        rule.Set(IntentionLanguage.LabelSlot, "Extract to the library");
        rule.Set(IntentionLanguage.DescriptionSlot, "Moves this step into the library and leaves a call here.");

        var action = model.NewChild(rule, IntentionLanguage.ActionSlot, IntentionConceptKeys.Extract);

        action.Set(IntentionLanguage.IntoConceptSlot, into);
        action.Set(IntentionLanguage.IntoChildSlot, slot);
        action.Set(IntentionLanguage.AsConceptSlot, asConcept);
        action.Set(IntentionLanguage.ReferenceSlot, reference);

        if (name is not null)
        {
            action.Set(IntentionLanguage.ExtractNameSlot, name);
        }

        return rule;
    }

    /// <summary>Lowers one extract rule against the extract fixture, which is where its checks run.</summary>
    private static void LowerExtract(
        string conceptKey = ExtractLanguage.Step,
        string into = ExtractLanguage.Container,
        string slot = "library",
        string asConcept = ExtractLanguage.Call,
        string reference = "calls",
        string? name = null) =>
        IntentionLowering.CreateProvider(
            [ExtractRule(conceptKey, into, slot, asConcept, reference, name)],
            ExtractLanguage.Concepts);

    private static (EditorSession Session, Node Rule, Node Condition) Document()
    {
        var model = PayrollSample.NewSampleModel();
        var rule = Assert.Single(model.Roots[0].GetChildren(RulesLanguage.RulesSlot));

        var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.AddIntentions(
            IntentionLowering.CreateProvider([Rule().Rule], ExpressionsLanguage.Concepts),
            IntentionSource.Language);

        return (session, rule, rule.GetChild("condition")!);
    }

    /// <summary>The negation as a document: wrap any expression in a <c>Unary</c> born with <c>operator = "!"</c>.</summary>
    private static (Model Model, Node Rule) Rule(
        string conceptKey = ExpressionsConceptKeys.Expression,
        (string Slot, string Value)? preset = null)
    {
        var model = IntentionLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(IntentionLanguage.ConceptKeySlot, conceptKey);
        rule.Set(IntentionLanguage.LabelSlot, "Negate");
        rule.Set(IntentionLanguage.DescriptionSlot, "Surrounds this expression with a negation.");

        var action = Assert.Single(rule.GetChildren(IntentionLanguage.ActionSlot));

        action.Set(IntentionLanguage.WrapperSlot, ExpressionsConceptKeys.Unary);
        action.Set(IntentionLanguage.WrapperChildSlot, "operand");

        var (slot, value) = preset ?? ("operator", "!");

        var entry = model.NewChild(action, IntentionLanguage.PresetSlot, IntentionConceptKeys.Preset);
        entry.Set(IntentionLanguage.PresetPropertySlot, slot);
        entry.Set(IntentionLanguage.PresetValueSlot, value);

        return (model, rule);
    }

    private static void Lower(string conceptKey = ExpressionsConceptKeys.Expression, (string Slot, string Value)? preset = null) =>
        IntentionLowering.CreateProvider([Rule(conceptKey, preset).Rule], ExpressionsLanguage.Concepts);

    private static void Caret(EditorSession session, Node node)
    {
        var place = CaretPlaces.Of(session.Tree).First(each => each.Address.Owner == node.Id);

        session.Select(Selection.At(CaretPosition.At(place, 0)));
    }
}
