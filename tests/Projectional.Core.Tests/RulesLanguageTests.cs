using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Samples;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The sample language is the proof that a host composes a language out of the shared expression
/// vocabulary instead of re-declaring it, so these tests are about the seam: that
/// <c>Rules</c> owns nothing of <c>Expressions</c>, and that what the design documents show is what
/// the model draws.
/// </summary>
public sealed class RulesLanguageTests
{
    private static NotationRegistry Notations => RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    [Fact]
    public void TheLanguagesConceptsAreItsOwnPlusTheExpressionsItBorrows()
    {
        Assert.Equal(
            RulesLanguage.ConceptDeclarations.Count
                + ExpressionsLanguage.ConceptDeclarations.Count
                + FrameworkInterfaces.All.Count,
            RulesLanguage.Concepts.Concepts.Count);

        foreach (var declaration in RulesLanguage.ConceptDeclarations)
        {
            Assert.True(RulesLanguage.Concepts.TryGet(declaration.Key, out _), declaration.Key);
        }
    }

    [Fact]
    public void AConditionIsAnExpressionWhichIsAbstractSoOnlyItsConcreteFormsStandThere()
    {
        Assert.True(RulesLanguage.Concepts.Get(ExpressionsConceptKeys.Expression).IsAbstract);
        Assert.Equal(
            ExpressionsConceptKeys.IntLiteral,
            RulesLanguage.Concepts.DefaultInstantiableFor(ExpressionsConceptKeys.Expression)?.Key);

        Assert.Equal(
            ExpressionsConceptKeys.Expression,
            RulesLanguage.Concepts.Get(RulesConceptKeys.Rule).FindChild("condition")?.ChildConceptKey);
    }

    [Fact]
    public void EveryConcreteExpressionCanStandInTheConditionSlot()
    {
        var concrete = RulesLanguage.Concepts.Concepts
            .Where(concept => concept.IsKindOf(ExpressionsConceptKeys.Expression) && !concept.IsAbstract)
            .ToList();

        Assert.NotEmpty(concrete);

        foreach (var concept in concrete)
        {
            var model = PayrollSample.NewEmptyModel();
            var rule = model.NewChild(model.Roots[0], "rules", RulesConceptKeys.Rule);

            Assert.Equal(concept.Key, model.NewChild(rule, "condition", concept.Key).Concept.Key);
        }
    }

    [Fact]
    public void ARulesetDrawsItsRulesAsAnIndentedBlock()
    {
        Assert.Equal(
            """
            ruleset Payroll {
              rule Approved when amount > 1000 then Accept
            }
            """,
            LayoutSnapshot.Render(PayrollSample.NewSampleModel(), Notations, width: 60));
    }

    [Fact]
    public void ARulesetDrawsOneRulePerLine()
    {
        var model = PayrollSample.NewSampleModel();
        var ruleset = model.Roots[0];
        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        rule.Set("name", "Vendor");
        rule.Set("outcome", RuleOutcomes.Reject);

        Assert.Equal(
            """
            ruleset Payroll {
              rule Approved when amount > 1000 then Accept
              rule Vendor when <empty> then Reject
            }
            """,
            LayoutSnapshot.Render(model, Notations, width: 60));
    }

    [Fact]
    public void AnEmptyRulesetDrawsItsSlotPlaceholderInsteadOfAVanishingBlock()
    {
        Assert.Equal(
            """
            ruleset unnamed {
              <empty>
            }
            """,
            LayoutSnapshot.Render(PayrollSample.NewEmptyModel(), Notations, width: 60));
    }

    [Fact]
    public void ARuleWhoseConditionIsNotFilledYetStillReadsAsARule()
    {
        var model = PayrollSample.NewEmptyModel();
        var rule = model.NewChild(model.Roots[0], "rules", RulesConceptKeys.Rule);
        rule.Set("name", "Rejected");
        rule.Set("outcome", RuleOutcomes.Reject);

        Assert.Equal(
            """
            ruleset unnamed {
              rule Rejected when <empty> then Reject
            }
            """,
            LayoutSnapshot.Render(model, Notations, width: 60));
    }
}
