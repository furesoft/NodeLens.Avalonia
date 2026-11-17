using Projectional.Core;
using Projectional.Language;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Semantics;
using Projectional.TypeRules;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The type-rule language: a rule written as a model lowers into the same <see cref="ITypeRule"/> a C#
/// author would have written.
/// </summary>
public sealed class TypeRuleLoweringTests
{
    [Fact]
    public void AFixedRuleAnswersItsType()
    {
        var registry = Lower(rules => rules.Rule("Expressions.IntLiteral", rules.Fixed("int")));
        var literal = Literal();

        Assert.Equal("int", registry.TypeOf(literal).Name);
    }

    [Fact]
    public void AChildRuleAnswersTheChildsType()
    {
        var registry = Lower(rules =>
        {
            rules.Rule("Expressions.IntLiteral", rules.Fixed("int"));
            rules.Rule("Expressions.Parenthesized", rules.Child("inner"));
        });

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");
        var parenthesized = model.NewNode(ExpressionsConceptKeys.Parenthesized);
        model.NewChild(parenthesized, "inner", ExpressionsConceptKeys.IntLiteral);

        Assert.Equal("int", registry.TypeOf(parenthesized).Name);
    }

    [Fact]
    public void ASwitchPicksByPropertyValueAndFallsBackToOtherwise()
    {
        var registry = Lower(rules =>
        {
            rules.Rule("Expressions.IntLiteral", rules.Fixed("int"));
            rules.Rule("Expressions.Binary", rules.Switch(
                "operator",
                [(">", rules.Fixed("bool")), ("+", rules.Child("left"))],
                rules.Fixed("string")));
        });

        var model = ExpressionsLanguage.NewProject().NewModel("expressions");

        Assert.Equal("bool", registry.TypeOf(Binary(model, ">")).Name);
        Assert.Equal("int", registry.TypeOf(Binary(model, "+")).Name);
        Assert.Equal("string", registry.TypeOf(Binary(model, "*")).Name);
    }

    [Fact]
    public void ARuleWithoutABodyIsALoadTimeError()
    {
        var model = TypeRuleLanguage.NewModel();
        var document = model.NewNode(TypeRuleConceptKeys.Rules);
        var rule = model.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(document));

        Assert.Contains("no body", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATypeTheFrameworkDoesNotHaveIsALoadTimeError()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => Lower(rules => rules.Rule("Expressions.IntLiteral", rules.Fixed("decimal"))));

        Assert.Contains("decimal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASwitchThatDeclaresOneValueTwiceIsALoadTimeError()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(rules =>
            rules.Rule("Expressions.Binary", rules.Switch(
                "operator",
                [(">", rules.Fixed("bool")), (">", rules.Fixed("int"))],
                rules.Fixed("string")))));

        Assert.Contains("twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASwitchWithoutAFallbackIsALoadTimeError()
    {
        var model = TypeRuleLanguage.NewModel();
        var document = model.NewNode(TypeRuleConceptKeys.Rules);
        var rule = model.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);
        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.Binary");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Switch);
        body.Set(TypeRuleLanguage.PropertySlot, "operator");

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(document));

        Assert.Contains("any other value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDeclaredTwinOfTheSampleTypesAgreesWithIt()
    {
        // The parity rule of ADR 0014, applied to types: the same rules written as a model and as C#
        // must answer the same thing about the same document.
        var declared = Lower(rules =>
        {
            rules.Rule("Expressions.IntLiteral", rules.Fixed("int"));
            rules.Rule("Expressions.BoolLiteral", rules.Fixed("bool"));
            rules.Rule("Expressions.StringLiteral", rules.Fixed("string"));
            rules.Rule("Expressions.Parenthesized", rules.Child("inner"));
            rules.Rule("Expressions.If", rules.Child("then"));
            rules.Rule("Expressions.Binary", rules.Switch(
                "operator",
                [
                    ("==", rules.Fixed("bool")),
                    ("!=", rules.Fixed("bool")),
                    ("<", rules.Fixed("bool")),
                    (">", rules.Fixed("bool")),
                    ("<=", rules.Fixed("bool")),
                    (">=", rules.Fixed("bool")),
                    ("&&", rules.Fixed("bool")),
                    ("||", rules.Fixed("bool")),
                ],
                rules.Child("left")));
            rules.Rule("Expressions.Unary", rules.Switch(
                "operator",
                [("!", rules.Fixed("bool"))],
                rules.Child("operand")));
        });

        var model = PayrollSample.NewSampleModel();

        foreach (var node in Nodes(model))
        {
            Assert.Equal(ExpressionsLanguage.Types.TypeOf(node).Name, declared.TypeOf(node).Name);
        }
    }

    private static IEnumerable<Node> Nodes(Model model)
    {
        foreach (var root in model.Roots)
        {
            foreach (var node in Walk(root))
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            foreach (var descendant in Walk(child))
            {
                yield return descendant;
            }
        }
    }

    private static Node Literal()
    {
        var model = ExpressionsLanguage.NewProject().NewModel("expressions");

        return model.NewNode(ExpressionsConceptKeys.IntLiteral);
    }

    private static Node Binary(Model model, string op)
    {
        var binary = model.NewNode(ExpressionsConceptKeys.Binary);

        binary.Set("operator", op);
        model.NewChild(binary, "left", ExpressionsConceptKeys.IntLiteral);

        return binary;
    }

    private static TypeRegistry Lower(Action<RuleBuilder> declare)
    {
        var builder = new RuleBuilder();

        declare(builder);

        return TypeRuleLowering.CreateRegistry(builder.Document);
    }

    /// <summary>Writes type rules the way a language author would: one model, rules created through it.</summary>
    private sealed class RuleBuilder
    {
        private readonly Model _model = TypeRuleLanguage.NewModel();

        public RuleBuilder() => Document = _model.NewNode(TypeRuleConceptKeys.Rules);

        public Node Document { get; }

        public void Rule(string conceptKey, Node body)
        {
            var rule = _model.NewChild(Document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);

            rule.Set(TypeRuleLanguage.ConceptKeySlot, conceptKey);
            rule.AddChild(TypeRuleLanguage.BodySlot, body);
        }

        public Node Fixed(string typeName)
        {
            var body = _model.NewNode(TypeRuleConceptKeys.Fixed);

            body.Set(TypeRuleLanguage.TypeNameSlot, typeName);

            return body;
        }

        public Node Child(string slot)
        {
            var body = _model.NewNode(TypeRuleConceptKeys.Child);

            body.Set(TypeRuleLanguage.SlotSlot, slot);

            return body;
        }

        public Node Switch(string property, (string Value, Node Body)[] cases, Node otherwise)
        {
            var body = _model.NewNode(TypeRuleConceptKeys.Switch);

            body.Set(TypeRuleLanguage.PropertySlot, property);

            foreach (var (value, caseBody) in cases)
            {
                var branch = _model.NewChild(body, TypeRuleLanguage.CaseSlot, TypeRuleConceptKeys.Case);

                branch.Set(TypeRuleLanguage.ValueSlot, value);
                branch.AddChild(TypeRuleLanguage.BodySlot, caseBody);
            }

            body.AddChild(TypeRuleLanguage.OtherwiseSlot, otherwise);

            return body;
        }
    }
}
