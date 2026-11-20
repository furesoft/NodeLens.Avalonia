using Projectional.Core;
using Projectional.Projection;
using Projectional.TypeRules;
using Xunit;
namespace Projectional.Core.Tests;

/// <summary>
/// The type-rule language's own notation: a rule document draws in the shape MPS writes a typesystem.
/// </summary>
public sealed class TypeRuleNotationTests
{
    [Fact]
    public void ARuleDocumentDrawsItsHeaderAndBody()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");

        Assert.Equal(
            """
            rule Expressions.IntLiteral is
              type int
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }

    [Fact]
    public void ASwitchDrawsItsCasesAndItsFallback()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.Binary");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Switch);
        body.Set(TypeRuleLanguage.PropertySlot, "operator");

        var branch = model.NewChild(body, TypeRuleLanguage.CaseSlot, TypeRuleConceptKeys.Case);
        branch.Set(TypeRuleLanguage.ValueSlot, ">");
        var caseBody = model.NewChild(branch, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        model.NewChild(caseBody, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "bool");

        model.NewChild(body, TypeRuleLanguage.OtherwiseSlot, TypeRuleConceptKeys.Child)
            .Set(TypeRuleLanguage.SlotSlot, "left");

        Assert.Equal(
            """
            rule Expressions.Binary is
              switch on operator
                when > then type bool
                otherwise:
                  type of left
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }

    [Fact]
    public void ARecordDrawsTheWayItsTypeIsWritten()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        var record = model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Record);

        var amount = model.NewChild(record, TypeRuleLanguage.FieldsSlot, TypeRuleConceptKeys.Field);
        amount.Set(TypeRuleLanguage.NameSlot, "amount");
        model.NewChild(amount, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");

        var currency = model.NewChild(record, TypeRuleLanguage.FieldsSlot, TypeRuleConceptKeys.Field);
        currency.Set(TypeRuleLanguage.NameSlot, "currency");

        // A list of optionals, so the nesting is drawn too.
        var list = model.NewChild(currency, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.List);
        var optional = model.NewChild(list, TypeRuleLanguage.ElementSlot, TypeRuleConceptKeys.Optional);
        model.NewChild(optional, TypeRuleLanguage.InnerSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "string");

        Assert.Equal(
            """
            rule Expressions.IntLiteral is
              type (amount: int, currency: [string?])
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }

    [Fact]
    public void ATypeExpressionDrawsExactlyWhatTheTypeIsCalled()
    {
        // The invariant that keeps the two halves honest: a finding quotes IType.Name, so a language author
        // reading the rule and a user reading the finding have to see the same text.
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, TypeRuleConceptKeys.Rule);

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        var optional = model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Optional);
        var list = model.NewChild(optional, TypeRuleLanguage.InnerSlot, TypeRuleConceptKeys.List);
        var record = model.NewChild(list, TypeRuleLanguage.ElementSlot, TypeRuleConceptKeys.Record);
        var field = model.NewChild(record, TypeRuleLanguage.FieldsSlot, TypeRuleConceptKeys.Field);

        field.Set(TypeRuleLanguage.NameSlot, "amount");
        model.NewChild(field, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");

        var drawn = LayoutSnapshot.Render(optional, TypeRuleNotations.Registry, width: 60).Trim();
        var lowered = TypeRuleLowering.CreateRegistry(model.Roots).TypeOf(model.Roots[0]).Name;

        Assert.Equal("[(amount: int)]?", drawn);
        Assert.Equal(lowered, drawn);
    }

    [Fact]
    public void ADeclaredTypeDrawsItsNameAndReadsItBack()
    {
        // A named type is a document of its own, beside the rules that refer to it: two roots, two files, one
        // registry when the language project lowers them together.
        var name = TypeRuleLanguage.NewProject().NewModel("Amount");
        var amount = name.NewNode(TypeRuleConceptKeys.Named);

        name.AddRoot(amount);
        amount.Set(TypeRuleLanguage.NameSlot, "Amount");
        name.NewChild(amount, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");

        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, TypeRuleConceptKeys.Named);

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Ref)
            .Set(TypeRuleLanguage.NameSlot, "Amount");

        Assert.Equal(
            """
            rule TypeRule.Named is
              type Amount
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));

        Assert.Equal("type Amount = int", LayoutSnapshot.Render(amount, TypeRuleNotations.Registry, width: 60));

        // The name is what the finding quotes, which is the word the author wrote.
        Assert.Equal("Amount", TypeRuleLowering.CreateRegistry([amount, rule]).TypeOf(amount).Name);
    }

    [Fact]
    public void AnEquationsBodyDrawsItsRelationsTheWayTheEquationReads()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var equations = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Equations);

        var equal = model.NewChild(equations, TypeRuleLanguage.EquationsSlot, TypeRuleConceptKeys.Equation);
        equal.Set(TypeRuleLanguage.RelationSlot, "==");
        model.NewChild(equal, TypeRuleLanguage.LeftSlot, TypeRuleConceptKeys.TypeVariable)
            .Set(TypeRuleLanguage.NameSlot, "element");
        model.NewChild(equal, TypeRuleLanguage.RightSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");

        var subtype = model.NewChild(equations, TypeRuleLanguage.EquationsSlot, TypeRuleConceptKeys.Equation);
        subtype.Set(TypeRuleLanguage.RelationSlot, "<:");
        model.NewChild(subtype, TypeRuleLanguage.LeftSlot, TypeRuleConceptKeys.TypeVariable)
            .Set(TypeRuleLanguage.NameSlot, "element");
        model.NewChild(subtype, TypeRuleLanguage.RightSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "string");

        var drawn = LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60);

        // A relation is written with the symbol TypeEquation.ToString() prints, so the rule and the sentence a
        // failed solve quotes are the same text.
        Assert.Contains("?element == int", drawn, StringComparison.Ordinal);
        Assert.Contains("?element <: string", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEquationsBodyWithNoRelationsDrawsTheEmptyPlaceholder()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");
        model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Equations);

        Assert.Contains("<< ... >>", LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60), StringComparison.Ordinal);
    }

    [Fact]
    public void AJoinDrawsAsTheOperatorItsNameUses()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        var join = model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Join);

        model.NewChild(join, TypeRuleLanguage.LeftSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");
        model.NewChild(join, TypeRuleLanguage.RightSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "string");

        Assert.Equal(
            """
            rule Expressions.IntLiteral is
              type int|string
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }

    [Fact]
    public void AMeetDrawsAsTheOperatorItsNameUses()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        var meet = model.NewChild(body, TypeRuleLanguage.TypeSlot, TypeRuleConceptKeys.Meet);

        model.NewChild(meet, TypeRuleLanguage.LeftSlot, TypeRuleConceptKeys.Primitive)
            .Set(TypeRuleLanguage.NameSlot, "int");
        model.NewChild(meet, TypeRuleLanguage.RightSlot, TypeRuleConceptKeys.Ref)
            .Set(TypeRuleLanguage.NameSlot, "Amount");

        Assert.Equal(
            """
            rule Expressions.IntLiteral is
              type int&Amount
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }

    [Fact]
    public void AComplaintDrawsItsSeverityAndItsMessage()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Complain);
        body.Set(TypeRuleLanguage.SeveritySlot, "error");
        body.Set(TypeRuleLanguage.MessageSlot, "An integer literal is not allowed here.");

        Assert.Equal(
            """
            rule Expressions.IntLiteral is
              report error "An integer literal is not allowed here."
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 80));
    }

    [Fact]
    public void AConditionalComplaintDrawsTheConditionAfterTheMessage()
    {
        var model = TypeRuleLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.Binary");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Complain);
        body.Set(TypeRuleLanguage.SeveritySlot, "error");
        body.Set(TypeRuleLanguage.MessageSlot, "The operands are not comparable.");

        var condition = model.NewChild(body, TypeRuleLanguage.WhenSlot, TypeRuleConceptKeys.NotComparable);
        condition.Set(TypeRuleLanguage.LeftSlot, "left");
        condition.Set(TypeRuleLanguage.RightSlot, "right");

        Assert.Equal(
            """
            rule Expressions.Binary is
              report error "The operands are not comparable." type of left is not comparable with type of right
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 120));
    }

    [Fact]
    public void ASupertypesDocumentDrawsTheEdgeItDeclares()
    {
        var model = TypeRuleLanguage.NewProject().NewModel("supertypes");
        var edge = model.NewNode(TypeRuleConceptKeys.Supertypes);

        model.NewChild(edge, TypeRuleLanguage.SubtypeSlot, TypeRuleConceptKeys.Ref)
            .Set(TypeRuleLanguage.NameSlot, "Amount");
        model.NewChild(edge, TypeRuleLanguage.SupertypeSlot, TypeRuleConceptKeys.Ref)
            .Set(TypeRuleLanguage.NameSlot, "Money");

        Assert.Equal(
            """
            Amount <: Money
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }
}
