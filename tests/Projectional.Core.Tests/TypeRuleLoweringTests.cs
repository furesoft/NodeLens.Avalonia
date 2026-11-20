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
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(model.Roots));

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
        var rule = model.Roots[0];
        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.Binary");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Switch);
        body.Set(TypeRuleLanguage.PropertySlot, "operator");

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(model.Roots));

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
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        var record = model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Record);
        var field = model.NewChild(record, TypeRuleLanguage.FieldsSlot, TypeRuleConceptKeys.Field);

        field.Set(TypeRuleLanguage.NameSlot, "amount");

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(model.Roots));

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
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");
        model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(model.Roots));

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

    [Fact]
    public void AnEquationsRuleAnswersUnknownAndStatesItsRelations()
    {
        var registry = Lower(rules => rules.Rule(
            "Expressions.IntLiteral",
            rules.Equations(("==", rules.Variable("element"), rules.Primitive("int")))));

        var literal = Literal();

        Assert.Equal("unknown", registry.TypeOf(literal).Name);

        var equation = Assert.Single(registry.EquationsFor(literal));

        Assert.Equal("?element == int", equation.ToString());
    }

    [Fact]
    public void AVariableWrittenTwiceIsOneVariable()
    {
        var registry = Lower(rules => rules.Rule(
            "Expressions.IntLiteral",
            rules.Equations(
                ("==", rules.Variable("element"), rules.Primitive("int")),
                ("<:", rules.Variable("element"), rules.Primitive("string")))));

        TypeEquation[] equations = [.. registry.EquationsFor(Literal())];

        Assert.Equal(2, equations.Length);
        Assert.Same(equations[0].Left, equations[1].Left);
    }

    [Fact]
    public void ADeclaredRelationIsSolvedThroughItsRule()
    {
        var registry = Lower(rules => rules.Rule(
            "Expressions.IntLiteral",
            rules.Equations(("==", rules.Variable("element"), rules.Primitive("int")))));

        var equation = Assert.Single(registry.EquationsFor(Literal()));
        var solution = new TypeSolver().Solve([equation]);

        Assert.True(solution.Succeeded, solution.Failure);
        Assert.Equal("int", solution.Of(Assert.IsType<TypeVariable>(equation.Left)).Name);
    }

    [Fact]
    public void AnEquationsBodyThatStatesNothingIsALoadTimeError()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");
        rule.AddChild(TypeRuleLanguage.BodySlot, model.NewNode(TypeRuleConceptKeys.Equations));

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(model.Roots));

        Assert.Contains("states none", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEquationOfAVariableWithItselfIsALoadTimeError()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];
        var equations = model.NewNode(TypeRuleConceptKeys.Equations);
        var equation = model.NewChild(equations, TypeRuleLanguage.EquationsSlot, TypeRuleConceptKeys.Equation);
        var left = model.NewNode(TypeRuleConceptKeys.TypeVariable);
        var right = model.NewNode(TypeRuleConceptKeys.TypeVariable);

        left.Set(TypeRuleLanguage.NameSlot, "element");
        right.Set(TypeRuleLanguage.NameSlot, "element");

        equation.Set(TypeRuleLanguage.RelationSlot, "==");
        equation.AddChild(TypeRuleLanguage.LeftSlot, left);
        equation.AddChild(TypeRuleLanguage.RightSlot, right);

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");
        rule.AddChild(TypeRuleLanguage.BodySlot, equations);

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(model.Roots));

        Assert.Contains("relates to itself", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AVariableOutsideAnEquationsBodyIsALoadTimeError()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];
        var variable = model.NewNode(TypeRuleConceptKeys.TypeVariable);
        var body = model.NewNode(TypeRuleConceptKeys.Fixed);

        variable.Set(TypeRuleLanguage.NameSlot, "element");
        body.AddChild(TypeRuleLanguage.TypeSlot, variable);

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");
        rule.AddChild(TypeRuleLanguage.BodySlot, body);

        var exception = Assert.Throws<ConceptDefinitionException>(() => TypeRuleLowering.CreateRegistry(model.Roots));

        Assert.Contains("nothing constrains it", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownRelationIsRefusedWhenItIsWritten()
    {
        var model = TypeRuleLanguage.NewModel();
        var equations = model.NewNode(TypeRuleConceptKeys.Equations);
        var equation = model.NewChild(equations, TypeRuleLanguage.EquationsSlot, TypeRuleConceptKeys.Equation);

        // The relation is an enumeration, so the model refuses a spelling the lowering never sees: the
        // validation lives where the value is written, which is the earliest place an author can be told.
        var exception = Assert.Throws<ModelInvariantException>(
            () => equation.Set(TypeRuleLanguage.RelationSlot, "similar"));

        Assert.Contains("==, <:, :>", exception.Message, StringComparison.Ordinal);
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

        return TypeRuleLowering.CreateRegistry(builder.Roots);
    }

    /// <summary>
    /// Writes type rules the way a language author would: one rule per root, in one model the fixture owns.
    /// </summary>
    /// <remarks>
    /// A rule is the root of its own document in a language project; here the roots share one model, which is
    /// exactly what the lowering takes - the project, not the lowering, is what enforces one root per file.
    /// </remarks>
    private sealed class RuleBuilder
    {
        private readonly Model _model = TypeRuleLanguage.NewProject().NewModel("rules");

        /// <summary>The root of every rule and named type, in the order they were declared.</summary>
        public IReadOnlyList<Node> Roots => _model.Roots;

        public void Rule(string conceptKey, Node body)
        {
            var rule = _model.NewNode(TypeRuleConceptKeys.Rule);

            _model.AddRoot(rule);

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

        /// <summary>A body that states relations instead of answering a type.</summary>
        public Node Equations(params (string Relation, Node Left, Node Right)[] equations)
        {
            var body = _model.NewNode(TypeRuleConceptKeys.Equations);

            foreach (var (relation, left, right) in equations)
            {
                var equation = _model.NewChild(body, TypeRuleLanguage.EquationsSlot, TypeRuleConceptKeys.Equation);

                equation.Set(TypeRuleLanguage.RelationSlot, relation);
                equation.AddChild(TypeRuleLanguage.LeftSlot, left);
                equation.AddChild(TypeRuleLanguage.RightSlot, right);
            }

            return body;
        }

        /// <summary>A type expression the solver gives a type to.</summary>
        public Node Variable(string name)
        {
            var variable = _model.NewNode(TypeRuleConceptKeys.TypeVariable);

            variable.Set(TypeRuleLanguage.NameSlot, name);

            return variable;
        }
    }
}
