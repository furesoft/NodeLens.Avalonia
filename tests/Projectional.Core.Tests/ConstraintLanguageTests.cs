using Projectional.Core;
using Projectional.ConstraintRules;
using Projectional.Language;
using Projectional.Projection;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>The language a language's own rules are written in: its concepts, and how a rule is drawn.</summary>
public sealed class ConstraintLanguageTests
{
    [Fact]
    public void ANewConstraintDocumentHoldsOneRule()
    {
        var model = ConstraintLanguage.NewModel();

        Assert.Single(model.Roots);
        Assert.Equal(ConstraintConceptKeys.Rule, model.Roots[0].Concept.Key);
    }

    [Fact]
    public void EveryConcreteConceptHasANotation()
    {
        // A concept without a notation still opens, as a missing-notation cell - the one state a user cannot work
        // around, so the composition is checked rather than trusted.
        foreach (var concept in ConstraintLanguage.Concepts.Concepts.Where(each => !each.IsAbstract))
        {
            Assert.True(
                ConstraintNotations.Registry.TryGet(concept.Key, out _),
                $"'{concept.Key}' has no notation in the constraint language's registry");
        }
    }

    [Fact]
    public void AnEmptyConceptCellReadsAsEveryNode()
    {
        // The one place where an empty property is the interesting state rather than the unfinished one: a rule that
        // names no concept is a rule about every node, and the placeholder is what says so.
        var model = ConstraintLanguage.NewModel();

        var drawn = LayoutSnapshot.Render(model.Roots[0], ConstraintNotations.Registry, width: 80);

        Assert.Contains("constraint on every node", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void AWrittenRuleDrawsWhatItChecks()
    {
        var model = ConstraintLanguage.NewModel();
        var rule = model.Roots[0];

        rule.Set(ConstraintLanguage.ConceptKeySlot, "Rules.Rule");
        rule.Set(ConstraintLanguage.SeveritySlot, ConstraintSeverities.Warning);
        rule.Set(ConstraintLanguage.CodeSlot, "rules.condition.required");
        rule.Set(ConstraintLanguage.MessageSlot, "a rule needs a condition");

        model.NewChild(rule, ConstraintLanguage.BodySlot, ConstraintConceptKeys.EmptySlot)
            .Set(ConstraintLanguage.TargetSlot, "condition");

        var drawn = LayoutSnapshot.Render(rule, ConstraintNotations.Registry, width: 80);

        Assert.Contains("constraint on Rules.Rule", drawn, StringComparison.Ordinal);
        Assert.Contains("the slot condition is empty", drawn, StringComparison.Ordinal);
        Assert.Contains("report warning", drawn, StringComparison.Ordinal);
        Assert.Contains("a rule needs a condition", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryBodyKindIsOfferedInsideARule()
    {
        // The body slot is declared over the abstract concept, so completion offers the four kinds and never the
        // abstract one - the same rule the other definition languages follow.
        var rule = ConstraintLanguage.Concepts.Get(ConstraintConceptKeys.Rule);
        var body = rule.FindChild(ConstraintLanguage.BodySlot);

        Assert.NotNull(body);
        Assert.Equal(ConstraintConceptKeys.Body, body.ChildConceptKey);
        Assert.True(ConstraintLanguage.Concepts.Get(ConstraintConceptKeys.Body).IsAbstract);
    }
}
