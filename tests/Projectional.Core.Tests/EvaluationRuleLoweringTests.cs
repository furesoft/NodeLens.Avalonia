using Projectional.Core;
using Projectional.EvaluationRules;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>A language's meaning written as a model, lowered into the registry the interpreter runs.</summary>
/// <remarks>
/// The bodies are the whole language, so the proof is not "a rule was built" but "a document runs": the models below
/// describe the arithmetic of the expression vocabulary, and evaluating through them has to give the answers its
/// hand-written rules give. That agreement is what makes the declared path an equal of the coded one rather than a sketch.
/// </remarks>
public sealed class EvaluationRuleLoweringTests
{

    [Fact]
    public void ADeclaredRuleReadsAPropertyThroughItsDeclaredType()
    {
        var rules = new Rules();
        Property(rules.Rule(ExpressionsConceptKeys.IntLiteral), "value");

        var (model, literal) = Literal(42);

        Assert.Equal(new IntValue(42), Evaluate(rules.Lower(), model, literal));
    }

    [Fact]
    public void ADeclaredOperatorRuleAppliesTheFrameworkSemantics()
    {
        var rules = new Rules();
        Property(rules.Rule(ExpressionsConceptKeys.IntLiteral), "value");

        var sum = Operator(rules.Rule(ExpressionsConceptKeys.Binary), "operator");
        Child(sum, "left", EvaluationRuleLanguage.LeftSlot);
        Child(sum, "right", EvaluationRuleLanguage.RightSlot);

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var node = model.NewNode(ExpressionsConceptKeys.Binary);
        node.Set("operator", "+");
        model.NewChild(node, "left", ExpressionsConceptKeys.IntLiteral).Set("value", 2);
        model.NewChild(node, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 3);

        Assert.Equal(new IntValue(5), Evaluate(rules.Lower(), model, node));
    }

    [Fact]
    public void ADeclaredOperatorRuleWithoutARightOperandIsUnary()
    {
        var rules = new Rules();
        Property(rules.Rule(ExpressionsConceptKeys.IntLiteral), "value");

        var negation = Operator(rules.Rule(ExpressionsConceptKeys.Unary), "operator");
        Child(negation, "operand", EvaluationRuleLanguage.LeftSlot);

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var node = model.NewNode(ExpressionsConceptKeys.Unary);
        node.Set("operator", "-");
        model.NewChild(node, "operand", ExpressionsConceptKeys.IntLiteral).Set("value", 7);

        Assert.Equal(new IntValue(-7), Evaluate(rules.Lower(), model, node));
    }

    [Fact]
    public void AnOperandIsABodyRatherThanASlotName()
    {
        // `(left * right) + 1`: the operands are bodies of their own, so an expression the node does not hold as two
        // children can still be computed - which a vocabulary of slot names could not say.
        var rules = new Rules();
        Property(rules.Rule(ExpressionsConceptKeys.IntLiteral), "value");

        var sum = Operator(rules.Rule(ExpressionsConceptKeys.Binary), "operator");

        var product = Body(sum, EvaluationRuleLanguage.LeftSlot, EvaluationRuleConceptKeys.Operator);
        product.Set(EvaluationRuleLanguage.OperatorSlot, "operator");
        Child(product, "left", EvaluationRuleLanguage.LeftSlot);
        Child(product, "right", EvaluationRuleLanguage.RightSlot);

        Constant(sum, "int", "1", EvaluationRuleLanguage.RightSlot);

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var node = model.NewNode(ExpressionsConceptKeys.Binary);
        node.Set("operator", "+");
        model.NewChild(node, "left", ExpressionsConceptKeys.IntLiteral).Set("value", 2);
        model.NewChild(node, "right", ExpressionsConceptKeys.IntLiteral).Set("value", 3);

        // Both bodies read the node's own symbol, which is `+`: the inner one adds the two children (5) and the outer one
        // adds a constant the node does not hold at all (6). The point is the right operand is a body - a constant here,
        // an operator or a `first` elsewhere - which a vocabulary of slot names could not say.
        Assert.Equal(new IntValue(6), Evaluate(rules.Lower(), model, node));
    }

    [Fact]
    public void ADeclaredOperatorAppliesTheSymbolItsBodyNames()
    {
        // The node is an int literal, which has no operator property to read one from: the symbol has to come from the rule,
        // which is what lets a rule say "this value against 50" at all.
        var rules = new Rules();
        var comparison = Body(rules.Rule(ExpressionsConceptKeys.IntLiteral), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Operator);
        comparison.Set(EvaluationRuleLanguage.SymbolSlot, ">=");

        Constant(comparison, "int", "5", EvaluationRuleLanguage.LeftSlot);
        Constant(comparison, "int", "3", EvaluationRuleLanguage.RightSlot);

        var (model, literal) = Literal(42);

        Assert.Equal(new BoolValue(true), Evaluate(rules.Lower(), model, literal));
    }

    [Fact]
    public void ADeclaredOperatorPrefersItsOwnSymbolOverTheOneItsNodeHolds()
    {
        // The operatorSlot names the node's `value`, which holds "42" and is not an operator at all. Preferring the body's
        // symbol is what makes the two forms one rule: a symbol written in the rule, and a symbol that varies per node.
        var rules = new Rules();
        var comparison = Body(rules.Rule(ExpressionsConceptKeys.IntLiteral), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Operator);
        comparison.Set(EvaluationRuleLanguage.SymbolSlot, ">=");
        comparison.Set(EvaluationRuleLanguage.OperatorSlot, "value");

        Constant(comparison, "int", "5", EvaluationRuleLanguage.LeftSlot);
        Constant(comparison, "int", "3", EvaluationRuleLanguage.RightSlot);

        var (model, literal) = Literal(42);

        Assert.Equal(new BoolValue(true), Evaluate(rules.Lower(), model, literal));
    }

    [Fact]
    public void AnOperatorBodyThatNamesNoSymbolAndWhoseNodeHoldsNoneIsRefused()
    {
        var rules = new Rules();
        var comparison = Body(rules.Rule(ExpressionsConceptKeys.IntLiteral), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Operator);

        Constant(comparison, "int", "5", EvaluationRuleLanguage.LeftSlot);

        var (model, literal) = Literal(42);
        var failure = new Interpreter(rules.Lower()).Run(literal, cancellationToken: TestContext.Current.CancellationToken).Failure;

        Assert.NotNull(failure);
        Assert.Contains("names no symbol", failure.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void ADeclaredChooseRuleAnswersWithTheArmItsConditionChooses()
    {
        var rules = new Rules();
        Property(rules.Rule(ExpressionsConceptKeys.IntLiteral), "value");
        Property(rules.Rule(ExpressionsConceptKeys.BoolLiteral), "value");

        var choose = Body(rules.Rule(ExpressionsConceptKeys.If), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Choose);
        Child(choose, "condition", EvaluationRuleLanguage.ConditionSlot);
        Child(choose, "then", EvaluationRuleLanguage.ThenSlot);
        Child(choose, "else", EvaluationRuleLanguage.OtherwiseSlot);

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var node = model.NewNode(ExpressionsConceptKeys.If);
        model.NewChild(node, "condition", ExpressionsConceptKeys.BoolLiteral).Set("value", true);
        model.NewChild(node, "then", ExpressionsConceptKeys.IntLiteral).Set("value", 1);
        model.NewChild(node, "else", ExpressionsConceptKeys.IntLiteral).Set("value", 2);

        Assert.Equal(new IntValue(1), Evaluate(rules.Lower(), model, node));
    }

    [Fact]
    public void ADeclaredConstantAnswersWithTheValueWrittenIntoTheRule()
    {
        var rules = new Rules();
        Constant(rules.Rule(ExpressionsConceptKeys.IntLiteral), "int", "7");

        var (model, literal) = Literal(42);

        // Not the node's own value: the rule says what it answers, which is what an outcome needs.
        Assert.Equal(new IntValue(7), Evaluate(rules.Lower(), model, literal));
    }

    [Fact]
    public void ADeclaredBindBindsANameForTheRestOfTheRule()
    {
        // On a `Name` node, whose `name` property holds the text `total`: bind total to 21, then answer with the variable
        // the property names. Without the binding the node would have nothing to read, which is what the test that follows
        // covers.
        var rules = new Rules();
        var rule = rules.Rule(ExpressionsConceptKeys.Name);

        var bind = Body(rule, EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Bind);
        bind.Set(EvaluationRuleLanguage.NameSlot, "total");

        Constant(bind, "int", "21", EvaluationRuleLanguage.ValueSlot);
        Variable(bind, "name", EvaluationRuleLanguage.BodySlot);

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var node = model.NewNode(ExpressionsConceptKeys.Name);
        node.Set("name", "total");

        Assert.Equal(new IntValue(21), Evaluate(rules.Lower(), model, node));
    }

    [Fact]
    public void AVariableWhoseNamePropertyTheNodeDoesNotHaveIsRefused()
    {
        // A rule written against the wrong concept: the node has no `slot` property for the rule to read a name from, and
        // the refusal names that rather than the framework's KeyNotFoundException.
        var rules = new Rules();
        var rule = rules.Rule(ExpressionsConceptKeys.IntLiteral);
        Variable(rule, "total");

        var (model, literal) = Literal(42);
        var failure = new Interpreter(rules.Lower()).Run(literal, cancellationToken: TestContext.Current.CancellationToken).Failure;

        Assert.NotNull(failure);
        Assert.Contains("is not a property of", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredFirstAnswersWithTheEntryThatApplied()
    {
        var (fixture, _) = FirstRules();

        Assert.Equal(new IntValue(2), Evaluate(fixture.Lower(), Block(entries: [(false, 1), (true, 2)])));
    }

    [Fact]
    public void ADeclaredFirstRefusesWhenNoEntryApplies()
    {
        var (fixture, _) = FirstRules();

        // A rule that says nothing about the empty case is a refusal rather than a default: a value nobody stated would be
        // a value the author never wrote.
        var failure = new Interpreter(fixture.Lower()).Run(Block(entries: [(false, 1)]), cancellationToken: TestContext.Current.CancellationToken).Failure;

        Assert.NotNull(failure);
        Assert.Contains("no entry", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredFirstAnswersWithItsOtherwiseBodyWhenNoEntryApplies()
    {
        var (fixture, first) = FirstRules();
        Constant(first, "int", "0", EvaluationRuleLanguage.OtherwiseSlot);

        Assert.Equal(new IntValue(0), Evaluate(fixture.Lower(), Block(entries: [(false, 1)])));
    }

    [Fact]
    public void ADeclaredFailRefusesWithItsSentence()
    {
        var rules = new Rules();
        var fail = Body(rules.Rule(ExpressionsConceptKeys.IntLiteral), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Fail);
        fail.Set(EvaluationRuleLanguage.MessageSlot, "this literal has no value here.");

        var (_, literal) = Literal(42);
        var failure = new Interpreter(rules.Lower()).Run(literal, cancellationToken: TestContext.Current.CancellationToken).Failure;

        Assert.NotNull(failure);
        Assert.Equal("this literal has no value here.", failure.Message);
    }

    [Fact]
    public void ADeclaredRuleDescribesItselfForTheDocumentation()
    {
        var rules = new Rules();
        Property(rules.Rule(ExpressionsConceptKeys.IntLiteral), "value");

        var sum = Operator(rules.Rule(ExpressionsConceptKeys.Binary), "operator");
        Child(sum, "left", EvaluationRuleLanguage.LeftSlot);
        Child(sum, "right", EvaluationRuleLanguage.RightSlot);

        var registry = rules.Lower();

        // The sentence names the slots the rule reads, not the values it happened to see, so a page describes every
        // document the rule applies to.
        Assert.Equal(
            "The operator of the node applied to the values of its two operands.",
            registry.RuleFor(ExpressionsLanguage.Concepts.Get(ExpressionsConceptKeys.Binary))!.Description);
        Assert.Equal(
            "The value of its value property, read through its declared type.",
            registry.RuleFor(ExpressionsLanguage.Concepts.Get(ExpressionsConceptKeys.IntLiteral))!.Description);
    }

    [Fact]
    public void ADeclaredOperatorDescribesTheSymbolItNames()
    {
        // A body that writes its own symbol describes it as written, and one that leaves it empty says where it reads it -
        // which is the difference a docs page has to show, because only the second varies with the node.
        var rules = new Rules();
        var comparison = Body(rules.Rule(ExpressionsConceptKeys.IntLiteral), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Operator);
        comparison.Set(EvaluationRuleLanguage.SymbolSlot, ">=");

        Constant(comparison, "int", "5", EvaluationRuleLanguage.LeftSlot);

        Assert.Equal(
            "Its >= operator applied to the value of its left operand.",
            rules.Lower().RuleFor(ExpressionsLanguage.Concepts.Get(ExpressionsConceptKeys.IntLiteral))!.Description);
    }
    [Fact]
    public void ADeclaredCountAnswersWithHowManyEntriesASlotHolds()
    {
        var rules = new Rules(FirstLanguage.Concepts);
        var count = Body(rules.Rule(FirstLanguage.Container), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Count);
        count.Set(EvaluationRuleLanguage.SlotSlot, FirstLanguage.EntriesSlot);

        Assert.Equal(new IntValue(3), Evaluate(rules.Lower(), Block(entries: [(true, 1), (false, 2), (true, 3)])));
    }

    [Fact]
    public void ADeclaredCountRefusesASlotTheConceptDoesNotDeclare()
    {
        // Zero would be a number a document could have meant, so a slot the concept does not have is a refusal naming the
        // concept - the rule was written against the wrong one.
        var rules = new Rules(FirstLanguage.Concepts);
        var count = Body(rules.Rule(FirstLanguage.Container), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Count);
        count.Set(EvaluationRuleLanguage.SlotSlot, "stops");

        var failure = new Interpreter(rules.Lower()).Run(Block(entries: [(true, 1)]), cancellationToken: TestContext.Current.CancellationToken).Failure;

        Assert.NotNull(failure);
        Assert.Contains("is not a child slot of", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredHasAnswersWhetherAPropertyHoldsAValue()
    {
        var rules = new Rules(GradeLanguage.Concepts);
        var has = Body(rules.Rule(GradeLanguage.Report), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Has);
        has.Set(EvaluationRuleLanguage.SlotSlot, GradeLanguage.PointsSlot);

        Assert.Equal(new BoolValue(true), Evaluate(rules.Lower(), Report(points: 42).Model.Roots[0]));
        Assert.Equal(new BoolValue(false), Evaluate(rules.Lower(), Report(points: null).Model.Roots[0]));
    }

    [Fact]
    public void ADeclaredHasGuardsASlotTheRuleOnlyReadsWhileItIsFilled()
    {
        // The composition the body exists for: `property` refuses on an empty slot and nothing else could stand in a
        // condition, so before it a rule that has to tolerate an empty slot could not be written at all.
        var rules = new Rules(GradeLanguage.Concepts);
        var rule = rules.Rule(GradeLanguage.Report);

        var choose = Body(rule, EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Choose);
        var condition = Body(choose, EvaluationRuleLanguage.ConditionSlot, EvaluationRuleConceptKeys.Has);
        condition.Set(EvaluationRuleLanguage.SlotSlot, GradeLanguage.NameSlot);
        Property(choose, GradeLanguage.NameSlot, EvaluationRuleLanguage.ThenSlot);
        Constant(choose, "string", "(unnamed)", EvaluationRuleLanguage.OtherwiseSlot);

        var registry = rules.Lower();

        Assert.Equal(new StringValue("essay"), Evaluate(registry, Report(points: 42, name: "essay").Model.Roots[0]));
        Assert.Equal(new StringValue("(unnamed)"), Evaluate(registry, Report(points: 42).Model.Roots[0]));
    }

    [Fact]
    public void ADeclaredHasRefusesASlotTheConceptDoesNotDeclare()
    {
        var rules = new Rules(GradeLanguage.Concepts);
        var has = Body(rules.Rule(GradeLanguage.Report), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Has);
        has.Set(EvaluationRuleLanguage.SlotSlot, "stops");

        var failure = new Interpreter(rules.Lower()).Run(Report(points: 42).Model.Roots[0], cancellationToken: TestContext.Current.CancellationToken).Failure;

        Assert.NotNull(failure);
        Assert.Contains("is not a slot of", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShapeBodiesDescribeWhatTheyAsk()
    {
        var asked = new Rules(GradeLanguage.Concepts);
        var has = Body(asked.Rule(GradeLanguage.Report), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Has);
        has.Set(EvaluationRuleLanguage.SlotSlot, GradeLanguage.NameSlot);

        var counted = new Rules(FirstLanguage.Concepts);
        var count = Body(counted.Rule(FirstLanguage.Container), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Count);
        count.Set(EvaluationRuleLanguage.SlotSlot, FirstLanguage.EntriesSlot);

        Assert.Equal(
            "Whether its name slot holds anything.",
            asked.Lower().RuleFor(GradeLanguage.Concepts.Get(GradeLanguage.Report))!.Description);
        Assert.Equal(
            "How many entries its entries child slot holds.",
            counted.Lower().RuleFor(FirstLanguage.Concepts.Get(FirstLanguage.Container))!.Description);
    }
    [Fact]
    public void ARuleThatNamesNoConceptIsRefused()
    {
        var rules = new Rules();
        Property(rules.Rule(conceptKey: null), "value");

        var exception = Assert.Throws<EvaluationRuleException>(() => rules.Lower());

        Assert.Contains("concept it answers for", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleThatNamesAConceptTheLanguageDoesNotDeclareIsRefused()
    {
        var rules = new Rules();
        Property(rules.Rule("Expressions.NoSuchConcept"), "value");

        Assert.Throws<ConceptDefinitionException>(() => rules.Lower());
    }

    [Fact]
    public void AConstantThatIsNotTheKindItClaimsIsRefused()
    {
        var rules = new Rules();
        Constant(rules.Rule(ExpressionsConceptKeys.IntLiteral), "int", "twelve");

        Assert.Throws<EvaluationRuleException>(() => rules.Lower());
    }

    /// <summary>The three rules a first body needs: a literal's value, a flag's value, and the block that walks entries.</summary>
    private static (Rules Rules, Node First) FirstRules()
    {
        var rules = new Rules(FirstLanguage.Concepts);

        Property(rules.Rule(FirstLanguage.Entry), FirstLanguage.OutcomeSlot);
        Property(rules.Rule(FirstLanguage.Entry), FirstLanguage.EnabledSlot);

        var first = Body(rules.Rule(FirstLanguage.Container), EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.First);
        first.Set(EvaluationRuleLanguage.OverSlot, FirstLanguage.EntriesSlot);
        Property(first, FirstLanguage.EnabledSlot, EvaluationRuleLanguage.WhenSlot);
        Property(first, FirstLanguage.OutcomeSlot, EvaluationRuleLanguage.ThenSlot);

        return (rules, first);
    }

    /// <summary>A block whose entries each carry an enabled flag and an outcome, which is the shape a first body walks.</summary>
    private static Node Block(params (bool Enabled, int Outcome)[] entries)
    {
        var model = new Project(FirstLanguage.Concepts).NewModel("first");
        var container = model.NewNode(FirstLanguage.Container);

        foreach (var (enabled, outcome) in entries)
        {
            var entry = model.NewChild(container, FirstLanguage.EntriesSlot, FirstLanguage.Entry);
            entry.Set(FirstLanguage.EnabledSlot, enabled);
            entry.Set(FirstLanguage.OutcomeSlot, outcome);
        }

        return container;
    }

    private static (Model Model, Node Literal) Literal(int value)
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var literal = model.NewNode(ExpressionsConceptKeys.IntLiteral);
        literal.Set("value", value);

        return (model, literal);
    }

    private static Value Evaluate(EvaluationRegistry rules, Model model, Node node)
    {
        _ = model;

        return Evaluate(rules, node);
    }

    private static Value Evaluate(EvaluationRegistry rules, Node node)
    {
        var result = new Interpreter(rules).Run(node, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Failure is null, result.Failure?.Message);

        return result.Value!;
    }

    /// <summary>Adds one body of <paramref name="conceptKey"/> to <paramref name="owner"/> and answers with it.</summary>
    /// <summary>A report with the points it scored, and optionally a name, in a model of its own.</summary>
    private static (Model Model, Node Report) Report(int? points, string? name = null)
    {
        var model = new Project(GradeLanguage.Concepts).NewModel("reports");
        var report = model.NewNode(GradeLanguage.Report);

        if (points is { } scored)
        {
            report.Set(GradeLanguage.PointsSlot, scored.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (name is not null)
        {
            report.Set(GradeLanguage.NameSlot, name);
        }

        model.AddRoot(report);

        return (model, report);
    }
    private static Node Body(Node owner, string slot, string conceptKey) =>
        owner.Model.NewChild(owner, slot, conceptKey);

    /// <summary>A body that reads one slot of the node: <paramref name="slot"/> holds the body, <paramref name="read"/> its name.</summary>
    private static Node Property(Node owner, string read, string slot = EvaluationRuleLanguage.BodySlot) =>
        Reading(owner, slot, EvaluationRuleConceptKeys.Property, read);

    private static Node Child(Node owner, string read, string slot = EvaluationRuleLanguage.BodySlot) =>
        Reading(owner, slot, EvaluationRuleConceptKeys.Child, read);

    private static Node Variable(Node owner, string read, string slot = EvaluationRuleLanguage.BodySlot) =>
        Reading(owner, slot, EvaluationRuleConceptKeys.Variable, read);

    /// <summary>A body that names a slot of the node in its own <c>slot</c> property.</summary>
    private static Node Reading(Node owner, string slot, string conceptKey, string read)
    {
        var body = Body(owner, slot, conceptKey);
        body.Set(EvaluationRuleLanguage.SlotSlot, read);

        return body;
    }

    private static Node Constant(Node owner, string kind, string value, string slot = EvaluationRuleLanguage.BodySlot)
    {
        var constant = Body(owner, slot, EvaluationRuleConceptKeys.Constant);
        constant.Set("kind", kind);
        constant.Set(EvaluationRuleLanguage.ValueSlot, value);

        return constant;
    }

    /// <summary>An operator body with its two operand slots, and no right operand until a test adds one.</summary>
    private static Node Operator(Node rule, string operatorSlot)
    {
        var op = Body(rule, EvaluationRuleLanguage.BodySlot, EvaluationRuleConceptKeys.Operator);
        op.Set(EvaluationRuleLanguage.OperatorSlot, operatorSlot);

        return op;
    }

    /// <summary>The rule models one test writes: one model, one rule per concept.</summary>
    private sealed class Rules
    {
        private readonly ConceptRegistry _concepts;
        private readonly Model _model = new Project(EvaluationRuleLanguage.Concepts).NewModel("rules");

        /// <summary>The rules of one test, checked against the language whose concepts they name.</summary>
        /// <param name="concepts">That language registry, or the expression vocabulary by default.</param>
        internal Rules(ConceptRegistry? concepts = null) => _concepts = concepts ?? ExpressionsLanguage.Concepts;
        private readonly List<Node> _roots = [];

        /// <summary>One rule for <paramref name="conceptKey"/>, or one with the property empty when it is <see langword="null"/>.</summary>
        internal Node Rule(string? conceptKey)
        {
            var rule = _model.NewNode(EvaluationRuleConceptKeys.Rule);

            if (conceptKey is not null)
            {
                rule.Set(EvaluationRuleLanguage.ConceptKeySlot, conceptKey);
            }

            _model.AddRoot(rule);
            _roots.Add(rule);

            return rule;
        }

        internal EvaluationRegistry Lower() => EvaluationRuleLowering.CreateRegistry(_roots, _concepts);
    }
}
