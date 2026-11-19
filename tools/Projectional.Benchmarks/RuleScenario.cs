using Projectional.Checks;
using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Semantics;

namespace Projectional.Benchmarks;

/// <summary>A rules document of <c>n</c> rules, which is the sample language and nothing else.</summary>
/// <remarks>
/// The document has no references, so it measures the part of checking that walks nodes: every rule asks two
/// constraints, and the standard catalogue looks at names, slots and empty nodes. It is the cheap half of the
/// comparison - the feature model is where a rule has to look something up.
/// </remarks>
internal sealed class RuleScenario : Scenario
{
    public override string Name => "rules";

    public override IReadOnlyList<int> Sizes => [100, 1_000, 5_000, 20_000];

    public override ConstraintSet Constraints { get; } = SampleConstraints.All.With(ScopeConstraints.All);

    public override CheckSet Checks => StandardChecks.All;

    public override TypeRegistry Types => ExpressionsLanguage.Types;

    public override NotationRegistry Notations { get; } = RulesLanguage.CreateNotations(RulesLanguage.Concepts);

    public override (Model Model, Node Root) Build(int units)
    {
        var model = RulesLanguage.NewProject().NewModel("bench");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        model.AddRoot(ruleset);
        ruleset.Set("name", "Payroll");

        for (var i = 1; i <= units; i++)
        {
            var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
            rule.Set("name", $"Rule{i}");
            rule.Set("outcome", RuleOutcomes.Accept);

            var condition = model.NewChild(rule, "condition", ExpressionsConceptKeys.BoolLiteral);
            condition.Set("value", true);
        }

        return (model, ruleset);
    }

    public override Node Representative(Node root) => root.GetChildren(RulesLanguage.RulesSlot)[0];
}
