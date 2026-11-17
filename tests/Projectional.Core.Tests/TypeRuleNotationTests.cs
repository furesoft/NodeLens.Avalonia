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
        var document = model.NewNode(TypeRuleConceptKeys.Rules);

        var rule = model.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);
        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.IntLiteral");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed);
        body.Set(TypeRuleLanguage.TypeNameSlot, "int");

        Assert.Equal(
            """
            types
              rule Expressions.IntLiteral is
                type int
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }

    [Fact]
    public void ASwitchDrawsItsCasesAndItsFallback()
    {
        var model = TypeRuleLanguage.NewModel();
        var document = model.NewNode(TypeRuleConceptKeys.Rules);

        var rule = model.NewChild(document, TypeRuleLanguage.RulesSlot, TypeRuleConceptKeys.Rule);
        rule.Set(TypeRuleLanguage.ConceptKeySlot, "Expressions.Binary");

        var body = model.NewChild(rule, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Switch);
        body.Set(TypeRuleLanguage.PropertySlot, "operator");

        var branch = model.NewChild(body, TypeRuleLanguage.CaseSlot, TypeRuleConceptKeys.Case);
        branch.Set(TypeRuleLanguage.ValueSlot, ">");
        model.NewChild(branch, TypeRuleLanguage.BodySlot, TypeRuleConceptKeys.Fixed)
            .Set(TypeRuleLanguage.TypeNameSlot, "bool");

        model.NewChild(body, TypeRuleLanguage.OtherwiseSlot, TypeRuleConceptKeys.Child)
            .Set(TypeRuleLanguage.SlotSlot, "left");

        Assert.Equal(
            """
            types
              rule Expressions.Binary is
                switch on operator
                  when > then type bool
                  otherwise:
                    type of left
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }

    [Fact]
    public void AnEmptyRulesDocumentShowsTheEmptySectionPlaceholder()
    {
        var model = TypeRuleLanguage.NewModel();
        model.NewNode(TypeRuleConceptKeys.Rules);

        Assert.Equal(
            """
            types
              << ... >>
            """,
            LayoutSnapshot.Render(model, TypeRuleNotations.Registry, width: 60));
    }
}
