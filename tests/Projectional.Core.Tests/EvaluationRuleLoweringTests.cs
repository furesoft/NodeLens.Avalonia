using Projectional.Core;
using Projectional.EvaluationRules;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A language's meaning written as a model, lowered into the registry the interpreter runs.</summary>
/// <remarks>
/// The five bodies are the whole language, so the proof is not "a rule was built" but "a document runs": the models below
/// describe the arithmetic of the expression vocabulary, and evaluating through them has to give the answers its
/// hand-written rules give. That agreement is what makes the declared path an equal of the coded one rather than a sketch.
/// </remarks>
public sealed class EvaluationRuleLoweringTests
{
    [Fact]
    public void ADeclaredRuleReadsAPropertyThroughItsDeclaredType()
    {
        var rules = Lower(Rule(ExpressionsConceptKeys.IntLiteral, Property("value")));
        var (model, literal) = Literal(42);

        Assert.Equal(new IntValue(42), Evaluate(rules, model, literal));
    }

    [Fact]
    public void ADeclaredOperatorRuleAppliesTheFrameworkSemantics()
    {
        var rules = Lower(
            Rule(ExpressionsConceptKeys.IntLiteral, Property("value")),
            Rule(ExpressionsConceptKeys.Binary, Operator("operator", "left", "right")));

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var sum = model.NewNode(ExpressionsConceptKeys.Binary);
        sum.Set("operator", "+");
        model.NewChild(sum, "left", ExpressionsConceptKeys.IntLiteral).Set("value", 2);
        model.NewChild(sum, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 3);

        Assert.Equal(new IntValue(5), Evaluate(rules, model, sum));
    }

    [Fact]
    public void ADeclaredUnaryRuleLeavesTheRightOperandOut()
    {
        var rules = Lower(
            Rule(ExpressionsConceptKeys.IntLiteral, Property("value")),
            Rule(ExpressionsConceptKeys.Unary, Operator("operator", "operand", right: null)));

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var negation = model.NewNode(ExpressionsConceptKeys.Unary);
        negation.Set("operator", "-");
        model.NewChild(negation, "operand", ExpressionsConceptKeys.IntLiteral).Set("value", 7);

        Assert.Equal(new IntValue(-7), Evaluate(rules, model, negation));
    }

    [Fact]
    public void ADeclaredChooseRuleAnswersWithTheArmItsConditionChooses()
    {
        var rules = Lower(
            Rule(ExpressionsConceptKeys.IntLiteral, Property("value")),
            Rule(ExpressionsConceptKeys.BoolLiteral, Property("value")),
            Rule(ExpressionsConceptKeys.If, Choose("condition", thenRead: "then", otherwiseRead: "else")));

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var chosen = model.NewNode(ExpressionsConceptKeys.If);
        model.NewChild(chosen, "condition", ExpressionsConceptKeys.BoolLiteral).Set("value", true);
        model.NewChild(chosen, "then", ExpressionsConceptKeys.IntLiteral).Set("value", 1);
        model.NewChild(chosen, "else", ExpressionsConceptKeys.IntLiteral).Set("value", 2);

        Assert.Equal(new IntValue(1), Evaluate(rules, model, chosen));
    }

    [Fact]
    public void ADeclaredRuleDescribesItselfForTheDocumentation()
    {
        var rules = Lower(
            Rule(ExpressionsConceptKeys.IntLiteral, Property("value")),
            Rule(ExpressionsConceptKeys.Binary, Operator("operator", "left", "right")));

        var binary = rules.RuleFor(ExpressionsLanguage.Concepts.Get(ExpressionsConceptKeys.Binary))!;
        var literal = rules.RuleFor(ExpressionsLanguage.Concepts.Get(ExpressionsConceptKeys.IntLiteral))!;

        // The sentence names the slots the rule reads, not the values it happened to see, so a page describes every
        // document the rule applies to.
        Assert.Equal("Its operator operator applied to the values of its left and right children.", binary.Description);
        Assert.Equal("The value of its value property, read through its declared type.", literal.Description);
    }

    [Fact]
    public void ARuleThatNamesNoConceptIsRefused()
    {
        var rules = RuleFor(conceptKey: null, Property("value"));

        var exception = Assert.Throws<EvaluationRuleException>(
            () => EvaluationRuleLowering.CreateRegistry([rules], ExpressionsLanguage.Concepts));

        Assert.Contains("concept it answers for", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleThatNamesAConceptTheLanguageDoesNotDeclareIsRefused()
    {
        var rules = RuleFor("Expressions.NoSuchConcept", Property("value"));

        Assert.Throws<ConceptDefinitionException>(
            () => EvaluationRuleLowering.CreateRegistry([rules], ExpressionsLanguage.Concepts));
    }

    private static EvaluationRegistry Lower(params Node[] rules) =>
        EvaluationRuleLowering.CreateRegistry(rules, ExpressionsLanguage.Concepts);

    private static (Model Model, Node Literal) Literal(int value)
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode(ExpressionsConceptKeys.IntLiteral);
        literal.Set("value", value);

        return (model, literal);
    }

    private static Value Evaluate(EvaluationRegistry rules, Model model, Node node)
    {
        var result = new Interpreter(rules).Run(node);

        Assert.True(result.Failure is null, result.Failure?.Message);

        return result.Value!;
    }

    /// <summary>One rule model: the concept it answers for, and the body it answers with.</summary>
    private static Node Rule(string conceptKey, Action<Node> body) => RuleFor(conceptKey, body);

    private static Node RuleFor(string? conceptKey, Action<Node> body)
    {
        var model = new Project(EvaluationRuleLanguage.Concepts).NewModel("rules");
        var rule = model.NewNode(EvaluationRuleConceptKeys.Rule);

        if (conceptKey is not null)
        {
            rule.Set(EvaluationRuleLanguage.ConceptKeySlot, conceptKey);
        }

        body(rule);
        model.AddRoot(rule);

        return rule;
    }

    private static Action<Node> Property(string slot) => rule => Body(rule, EvaluationRuleConceptKeys.Property)
        .Set(EvaluationRuleLanguage.SlotSlot, slot);

    private static Action<Node> Child(string slot) => rule => Body(rule, EvaluationRuleConceptKeys.Child)
        .Set(EvaluationRuleLanguage.SlotSlot, slot);

    private static Action<Node> Operator(string operatorSlot, string left, string? right) => rule =>
    {
        var op = Body(rule, EvaluationRuleConceptKeys.Operator);

        op.Set(EvaluationRuleLanguage.OperatorSlot, operatorSlot);
        op.Set(EvaluationRuleLanguage.LeftSlot, left);

        if (right is not null)
        {
            op.Set(EvaluationRuleLanguage.RightSlot, right);
        }
    };

    /// <summary>A choose body whose two arms read the children in <paramref name="thenRead"/> and <paramref name="otherwiseRead"/>.</summary>
    private static Action<Node> Choose(string conditionSlot, string thenRead, string otherwiseRead) => rule =>
    {
        var choose = Body(rule, EvaluationRuleConceptKeys.Choose);

        choose.Set(EvaluationRuleLanguage.ConditionSlot, conditionSlot);

        choose.Model
            .NewChild(choose, EvaluationRuleLanguage.ThenSlot, EvaluationRuleConceptKeys.Child)
            .Set(EvaluationRuleLanguage.SlotSlot, thenRead);

        choose.Model
            .NewChild(choose, EvaluationRuleLanguage.OtherwiseSlot, EvaluationRuleConceptKeys.Child)
            .Set(EvaluationRuleLanguage.SlotSlot, otherwiseRead);
    };

    private static Node Body(Node rule, string conceptKey) =>
        rule.Model.NewChild(rule, EvaluationRuleLanguage.BodySlot, conceptKey);
}
