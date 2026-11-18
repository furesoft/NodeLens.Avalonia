using Projectional.Avalonia;
using Projectional.Checks;
using Projectional.Core;
using Projectional.Expressions;
using Projectional.Language;
using Projectional.Samples;
using Projectional.Samples.Checks;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// The one list a shell shows: what the caller's checks found, what the language's constraints report, and the
/// framework's own ambiguity rule - merged, because a user must not see a squiggle the panel does not list or a
/// repair for a finding nobody drew.
/// </summary>
public sealed class DocumentFindingsTests
{
    [Fact]
    public void AConstraintFindingIsReportedButCarriesNoRepair()
    {
        var model = RuleWithoutCondition();

        var findings = DocumentFindings.Of(model, constraints: SampleConstraints.All);

        var finding = Assert.Single(findings.Diagnostics);

        Assert.Equal(SampleConstraints.MissingConditionCode, finding.Code);
        Assert.Equal([finding], findings.Markers.Diagnostics.ToArray());
        Assert.Equal(0, findings.Fixes.Count);
    }

    [Fact]
    public void AChecksRepairReachesTheListAndTheMarks()
    {
        var model = RuleWithoutCondition();
        var fixes = new PlaceholderNameCheck().Find(model);
        var repair = Assert.Single(fixes);

        var findings = DocumentFindings.Of(model, fixes, SampleConstraints.All);

        Assert.Contains(repair.Diagnostic, findings.Diagnostics);
        Assert.Contains(repair.Diagnostic, findings.Markers.Diagnostics);
        Assert.Contains(repair, findings.Fixes.For(repair.Diagnostic));
    }

    [Fact]
    public void TheFrameworkReportsAnAmbiguousNameEvenWhenTheLanguageDeclaresNoConstraints()
    {
        // Two tags with one label, and a step pointing at the first: the reference no longer identifies one
        // element. The rule is the framework's, so a language that declares no constraints still reports it.
        var model = TestLanguage.NewModel();
        var salary = NamedTag(model, "Salary");
        NamedTag(model, "Salary");

        var rule = model.NewNode(TestLanguage.Rule);
        rule.SetReference("tag", salary);
        model.AddRoot(rule);

        var findings = DocumentFindings.Of(model);

        Assert.Equal(
            [AmbiguousReferenceConstraint.Code],
            findings.Diagnostics.Select(finding => finding.Code));
        Assert.Equal(0, findings.Fixes.Count);
    }

    [Fact]
    public void ACleanDocumentHasNothingToShow()
    {
        var findings = DocumentFindings.Of(
            PayrollSample.NewSampleModel(),
            constraints: SampleConstraints.All,
            types: ExpressionsLanguage.Types);

        Assert.Empty(findings.Diagnostics);
        Assert.Equal(0, findings.Markers.Count);
        Assert.Equal(0, findings.Fixes.Count);
    }

    [Fact]
    public void ADocumentIsRequired() =>
        Assert.Throws<ArgumentNullException>(() => DocumentFindings.Of(null!));

    /// <summary>A root carrying <paramref name="label"/>, which is the name slot of `Test.Tag`.</summary>
    private static Node NamedTag(Model model, string label)
    {
        var tag = model.NewNode(TestLanguage.Tag);
        tag.Set("label", label);
        model.AddRoot(tag);

        return tag;
    }

    /// <summary>
    /// A ruleset holding one rule with no condition, whose name is still the concept's default - which is what the
    /// sample language reports as a missing condition and `PlaceholderNameCheck` as a name still waiting for one.
    /// </summary>
    private static Model RuleWithoutCondition()
    {
        var model = RulesLanguage.NewProject().NewModel("rules");
        var ruleset = model.NewNode(RulesConceptKeys.Ruleset);
        ruleset.Set("name", "Payroll");
        model.AddRoot(ruleset);

        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        return model;
    }
}
