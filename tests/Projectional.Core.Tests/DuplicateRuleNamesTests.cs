using Projectional.Core;
using Projectional.Editing;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The sample language's own check: two rules of one ruleset may not share a name, and the repair is a
/// rename to the first free suffix.
/// </summary>
/// <remarks>
/// It runs on a plain model, which is the point of it being here: finding a duplicate needs one child slot
/// and a set of names, so the whole quick-fix path is provable before any constraint engine exists. The
/// proposal test carries the subtle case - two rules with one name must not both be repaired to the same
/// new name.
/// </remarks>
public sealed class DuplicateRuleNamesTests
{
    [Fact]
    public void TheLaterOfTwoRulesWithOneNameIsReported()
    {
        var (first, second) = TwoRules("Check", "Check");

        var finding = Assert.Single(DuplicateRuleNames.Of(ModelOf(first)));

        Assert.Equal(second.Id, finding.Diagnostic.Target);
        Assert.Equal(DuplicateRuleNames.Code, finding.Diagnostic.Code);

        // The report sits under the word, not under the whole rule: the name is what is wrong.
        Assert.Equal("name", finding.Diagnostic.Role);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Diagnostic.Severity);
    }

    [Fact]
    public void ADocumentWhoseNamesDifferIsFine()
    {
        var (first, _) = TwoRules("Check", "Review");

        Assert.Empty(DuplicateRuleNames.Of(ModelOf(first)));
    }

    [Fact]
    public void NamesAreUniquePerRulesetNotPerModel()
    {
        // Two rulesets may each have a rule called "Check": a name is scoped to the set it belongs to.
        var model = RulesLanguage.NewProject().NewModel("payroll");

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
            model.AddRoot(ruleset);

            var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
            rule.Set("name", "Check");
        }

        Assert.Empty(DuplicateRuleNames.Of(model));
    }

    [Fact]
    public void TwoRulesWithOneNameGetTwoDifferentProposals()
    {
        // The naive repair - "append 2 to the name" - would propose the same new name twice, which only
        // works if the user applies the first one. The names already proposed count as taken.
        var (first, _) = TwoRules("Check", "Check", "Check");

        var labels = DuplicateRuleNames.Of(ModelOf(first)).Select(fix => fix.Label);

        Assert.Equal(["Rename to \"Check2\"", "Rename to \"Check3\""], labels);
    }

    [Fact]
    public void TheProposalSkipsANameSomebodyAlreadyUses()
    {
        var (first, _) = TwoRules("Check", "Check2", "Check");

        var label = Assert.Single(DuplicateRuleNames.Of(ModelOf(first))).Label;

        Assert.Equal("Rename to \"Check3\"", label);
    }

    [Fact]
    public void ApplyingTheFixLeavesTwoDistinctNames()
    {
        var (first, second) = TwoRules("Check", "Check");
        var model = ModelOf(first);
        var findings = DuplicateRuleNames.Of(model);

        // The session is opened on the ruleset, because that is the document: a rule addressed inside a
        // tree built from a sibling rule would be a different node, or no node at all.
        using var session = new EditorSession(
            model.Roots[0],
            RulesLanguage.CreateNotations(RulesLanguage.Concepts),
            typing: RulesLanguage.TypingRules);

        session.Fixes = new FixSet([.. findings]);

        var result = session.Apply(findings[0]);

        Assert.Equal(EditOutcome.Applied, result.Outcome);
        Assert.Equal("Check", first.GetOrNull("name"));
        Assert.Equal("Check2", second.GetOrNull("name"));

        // The finding is gone once the model says what the repair made it say.
        Assert.Empty(DuplicateRuleNames.Of(model));
    }

    [Fact]
    public void AFindingWhoseNodeIsGoneDeclinesInsteadOfThrowing()
    {
        // A fix outlives the model it was built for: it is applied through the session's current set, and
        // the node may be gone by then. Declining is the answer, not an exception inside a menu.
        var (first, _) = TwoRules("Check", "Check");
        var finding = Assert.Single(DuplicateRuleNames.Of(ModelOf(first)));

        using var session = new EditorSession(
            first,
            RulesLanguage.CreateNotations(RulesLanguage.Concepts),
            typing: RulesLanguage.TypingRules);

        session.Fixes = new FixSet(finding);

        Assert.Null(finding.Create(default));
    }

    [Fact]
    public void ANullModelIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => DuplicateRuleNames.Of(null!));
    }

    /// <summary>A ruleset holding one rule per name, on a model of its own.</summary>
    private static (Node First, Node Second) TwoRules(params string[] names)
    {
        var model = RulesLanguage.NewProject().NewModel("payroll");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        model.AddRoot(ruleset);

        var rules = names
            .Select(name =>
            {
                var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
                rule.Set("name", name);

                return rule;
            })
            .ToArray();

        return (rules[0], rules[1]);
    }

    private static Model ModelOf(Node node) => node.Model;
}
