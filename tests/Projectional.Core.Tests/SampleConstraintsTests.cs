using Projectional.Core;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The sample language's own constraints, which is the engine's first real consumer.
/// </summary>
public sealed class SampleConstraintsTests
{
    [Fact]
    public void ARuleWithoutAConditionIsReported()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");

        var rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        var finding = Assert.Single(new ConstraintEngine(SampleConstraints.All).Check(model));

        Assert.Equal(rule.Id, finding.Target);
        Assert.Equal("condition", finding.Role);
        Assert.Equal(SampleConstraints.MissingConditionCode, finding.Code);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
    }

    [Fact]
    public void TheSampleDocumentIsClean()
    {
        // The shipped sample has a condition, so the app must not greet a user with a finding on it.
        Assert.Empty(new ConstraintEngine(SampleConstraints.All).Check(PayrollSample.NewSampleModel()));
    }
}
