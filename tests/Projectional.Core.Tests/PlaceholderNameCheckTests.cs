using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD001</c>: a name that still equals the placeholder its concept starts it with.
/// </summary>
/// <remarks>
/// The placeholder comes from the declaration, never from a table in the check, so the tests use the sample
/// language's <c>"unnamed"</c> and the shared sub-language's absent default as the two cases that matter.
/// </remarks>
public sealed class PlaceholderNameCheckTests
{
    private static PlaceholderNameCheck Check => new();

    [Fact]
    public void ANewDocumentReportsItsRuleset()
    {
        var model = PayrollSample.NewEmptyModel();

        var fix = Assert.Single(Check.Find(model));
        var finding = fix.Diagnostic;

        Assert.Equal(PlaceholderNameCheck.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Hint, finding.Severity);
        Assert.Equal("name", finding.Role);
        Assert.Equal(model.Roots[0].Id, finding.Target);
        Assert.Equal("Name it \"Ruleset1\"", fix.Label);
    }

    [Fact]
    public void NamingItSilencesTheFinding()
    {
        var model = PayrollSample.NewEmptyModel();
        model.Roots[0].Set("name", "Payroll");

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AConceptThatDeclaresNoPlaceholderIsNotReported()
    {
        // Expressions.Name has a name slot but no default, so an empty name is one the user has not typed
        // yet rather than one they were meant to replace.
        var model = RulesLanguage.NewProject().NewModel("expressions");
        model.AddRoot(model.NewNode(ExpressionsConceptKeys.Name));

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void TwoUnnamedNodesAreOfferedDifferentNames()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        Assert.Equal(
            ["Name it \"Rule1\"", "Name it \"Rule2\""],
            Check.Find(model).Select(fix => fix.Label));
    }

    [Fact]
    public void AProposalAvoidsANameTheDocumentAlreadyUses()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule).Set("name", "Rule1");
        model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);

        Assert.Equal("Name it \"Rule2\"", Check.Find(model)[0].Label);
    }

    [Fact]
    public void TheRepairNamesTheNodeAndUndoTakesItBack()
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        var fix = Assert.Single(Check.Find(model));

        using var session = Open(model);
        Publish(session, fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);
        Assert.Equal("Ruleset1", ruleset.GetOrNull("name"));
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Equal("unnamed", ruleset.GetOrNull("name"));
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));
    }

    private static EditorSession Open(Model model) =>
        new(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));

    private static void Publish(EditorSession session, DiagnosticFix fix)
    {
        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);
    }
}
