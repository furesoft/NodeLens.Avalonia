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
    public void AFixedRuleCanNameARecord()
    {
        var registry = Lower(rules => rules.Rule(
            "Expressions.IntLiteral",
            rules.FixedAs(rules.Record(("amount", rules.Primitive("int")), ("currency", rules.Primitive("string"))))));

        Assert.Equal("(amount: int, currency: string)", registry.TypeOf(Literal()).Name);
    }

    [Fact]
    public void ATypeExpressionNests()
    {
        var registry = Lower(rules => rules.Rule(
            "Expressions.IntLiteral",
            rules.FixedAs(rules.ListOf(rules.OptionalOf(rules.Primitive("int"))))));

        Assert.Equal("[int?]", registry.TypeOf(Literal()).Name);
    }

    [Fact]
    public void ARecordFieldWithoutATypeIsALoadTimeError()
    {
        var model = TypeRuleLanguage.NewModel();
        var document = model.NewNode(TypeRuleConceptKeys.Rules);
        var rule = model.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        var record = model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Record);
        var field = model.NewChild(record, TypeRuleLanguage.FieldsSlot, TypeRuleConceptKeys.Field);

        field.Set(TypeRuleLanguage.NameSlot, "amount");

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(document));

        Assert.Contains("'type'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordThatNamesOneFieldTwiceIsALoadTimeError()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(rules => rules.Rule(
            "Expressions.IntLiteral",
            rules.FixedAs(rules.Record(
                ("amount", rules.Primitive("int")),
                ("amount", rules.Primitive("string")))))));

        // The field name, not "fields" - the parameter name of an exception the author never saw.
        Assert.Contains("'amount'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFixedBodyWithoutATypeIsALoadTimeError()
    {
        var model = TypeRuleLanguage.NewModel();
        var document = model.NewNode(TypeRuleConceptKeys.Rules);
        var rule = model.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");
        model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(document));

        Assert.Contains("'type'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleCanNameADeclaredType()
    {
        var registry = Lower(rules =>
        {
            rules.Declare("Amount", rules.Primitive("int"));
            rules.Rule("Expressions.IntLiteral", rules.FixedAs(rules.Ref("Amount")));
        });

        var type = registry.TypeOf(Literal());

        Assert.Equal("Amount", type.Name);
        Assert.Equal("int", Assert.IsType<NamedType>(type).Expansion.Name);
        Assert.True(type.Accepts(PrimitiveType.Of(PropertyType.Integer)));
    }

    [Fact]
    public void ADeclaredTypeCanBeWrittenOnceAndUsedInsideAnother()
    {
        var registry = Lower(rules =>
        {
            rules.Declare("Amount", rules.Primitive("int"));
            rules.Declare("Line", rules.Record(("amount", rules.Ref("Amount"))));
            rules.Rule("Expressions.IntLiteral", rules.FixedAs(rules.ListOf(rules.Ref("Line"))));
        });

        var type = registry.TypeOf(Literal());

        Assert.Equal("[Line]", type.Name);
        Assert.True(type.Accepts(new ListType(new RecordType([("amount", PrimitiveType.Of(PropertyType.Integer))]))));
    }

    [Fact]
    public void ATypeTheModelDoesNotDeclareIsALoadTimeError()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(
            () => Lower(rules => rules.Rule("Expressions.IntLiteral", rules.FixedAs(rules.Ref("Amount")))));

        Assert.Contains("Amount", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATypeDeclaredTwiceIsALoadTimeError()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(rules =>
        {
            rules.Declare("Amount", rules.Primitive("int"));
            rules.Declare("Amount", rules.Primitive("string"));
            rules.Rule("Expressions.IntLiteral", rules.FixedAs(rules.Ref("Amount")));
        }));

        Assert.Contains("twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATypeDeclaredInTermsOfItselfIsALoadTimeError()
    {
        // Not a recursive type: a definition with no base, which would expand forever.
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(rules =>
        {
            rules.Declare("Amount", rules.ListOf(rules.Ref("Amount")));
            rules.Rule("Expressions.IntLiteral", rules.FixedAs(rules.Ref("Amount")));
        }));

        Assert.Contains("in terms of itself", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATypeDeclaredThroughAnotherItselfLoopsBackAndIsALoadTimeError()
    {
        var exception = Assert.Throws<ConceptDefinitionException>(() => Lower(rules =>
        {
            rules.Declare("First", rules.ListOf(rules.Ref("Second")));
            rules.Declare("Second", rules.Ref("First"));
            rules.Rule("Expressions.IntLiteral", rules.FixedAs(rules.Ref("First")));
        }));

        Assert.Contains("in terms of itself", exception.Message, StringComparison.Ordinal);
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

            _model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
                .Set(TypeRuleLanguage.NameSlot, typeName);

            return body;
        }

        /// <summary>A fixed body holding an arbitrary type expression.</summary>
        public Node FixedAs(Node type)
        {
            var body = _model.NewNode(TypeRuleConceptKeys.Fixed);

            body.AddChild(TypeRuleLanguage.TypeSlot, type);

            return body;
        }

        public Node Record(params (string Name, Node Type)[] fields)
        {
            var record = _model.NewNode(TypeRuleConceptKeys.Record);

            foreach (var (name, type) in fields)
            {
                var field = _model.NewChild(record, TypeRuleLanguage.FieldsSlot, TypeRuleConceptKeys.Field);

                field.Set(TypeRuleLanguage.NameSlot, name);
                field.AddChild(TypeRuleLanguage.TypeSlot, type);
            }

            return record;
        }

        public Node Primitive(string name)
        {
            var primitive = _model.NewNode(TypeRuleConceptKeys.Primitive);

            primitive.Set(TypeRuleLanguage.NameSlot, name);

            return primitive;
        }

        public Node ListOf(Node element)
        {
            var list = _model.NewNode(TypeRuleConceptKeys.List);

            list.AddChild(TypeRuleLanguage.ElementSlot, element);

            return list;
        }

        public Node OptionalOf(Node inner)
        {
            var optional = _model.NewNode(TypeRuleConceptKeys.Optional);

            optional.AddChild(TypeRuleLanguage.InnerSlot, inner);

            return optional;
        }

        /// <summary>Declares a type under a name, as a root of the same model the rules live in.</summary>
        public Node Declare(string name, Node type)
        {
            var named = _model.NewNode(TypeRuleConceptKeys.Named);

            named.Set(TypeRuleLanguage.NameSlot, name);
            named.AddChild(TypeRuleLanguage.TypeSlot, type);

            return named;
        }

        /// <summary>A type expression that names a declared type.</summary>
        public Node Ref(string name)
        {
            var reference = _model.NewNode(TypeRuleConceptKeys.Ref);

            reference.Set(TypeRuleLanguage.NameSlot, name);

            return reference;
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
