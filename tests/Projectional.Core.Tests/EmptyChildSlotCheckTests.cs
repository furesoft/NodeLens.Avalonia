using Projectional.Checks;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;
using Projectional.Samples;
using Projectional.Semantics;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>
/// <c>STD010</c>: a single-valued child slot that holds nothing.
/// </summary>
/// <remarks>
/// The repair is <see cref="InsertChildAction"/>, which is what <c>Enter</c> runs, so these tests also pin
/// that a check and a keystroke cannot disagree about what belongs in a hole.
/// </remarks>
public sealed class EmptyChildSlotCheckTests
{
    private static EmptyChildSlotCheck Check => new();

    [Fact]
    public void AnEmptySingleValuedSlotIsReported()
    {
        var model = Document(out var rule);

        var fix = Assert.Single(Check.Find(model));
        var finding = fix.Diagnostic;

        Assert.Equal(EmptyChildSlotCheck.Code, finding.Code);
        Assert.Equal(DiagnosticSeverity.Hint, finding.Severity);
        Assert.Equal("condition", finding.Role);
        Assert.Equal(rule.Id, finding.Target);

        // The language's own default for the set the slot names, which is what Enter would create.
        Assert.Equal("Insert \"IntLiteral\"", fix.Label);
    }

    [Fact]
    public void AFilledSlotIsNotReported()
    {
        var model = Document(out var rule);
        model.NewChild(rule, "condition", ExpressionsConceptKeys.IntLiteral).Set("value", 1);

        Assert.Empty(Check.Find(model));
    }

    [Fact]
    public void AListThatMayHoldNothingIsNotReported()
    {
        // A list is not a hole: an empty ruleset is a document in progress.
        Assert.Empty(Check.Find(PayrollSample.NewEmptyModel()));
    }

    [Fact]
    public void ASingleChildSlotThatMayStayEmptyIsNotReported()
    {
        // `0..1` promises nothing, so an empty one is legal - the declaration says so, and the check reads it.
        Assert.Empty(Check.Find(CardinalityLanguage.NewModel()));
    }

    [Fact]
    public void ASlotWithNoConcreteConceptIsNotReported()
    {
        // The holder's "sealed" slot is declared over a set whose every member is abstract. There is no
        // repair, so there is no finding - inventing a node the language ruled out would be worse.
        var holder = AbstractSlotLanguage.NewHolder();

        var fix = Assert.Single(Check.Find(holder.Model));

        Assert.Equal("value", fix.Diagnostic.Role);
        Assert.Equal("Insert \"Number\"", fix.Label);
    }

    [Fact]
    public void TheRepairFillsTheSlotAsEnterWould()
    {
        var model = Document(out var rule);
        var fix = Assert.Single(Check.Find(model));

        using var session = new EditorSession(model.Roots[0], RulesLanguage.CreateNotations(RulesLanguage.Concepts));
        session.Markers = new MarkerSet(fix.Diagnostic);
        session.Fixes = new FixSet(fix);

        Assert.Equal(EditOutcome.Applied, session.Apply(fix).Outcome);

        var condition = Assert.Single(rule.GetChildren("condition"));

        Assert.Equal(ExpressionsConceptKeys.IntLiteral, condition.Concept.Key);
        Assert.Empty(Check.Find(model));

        Assert.True(session.Undo());
        Assert.Empty(rule.GetChildren("condition"));
    }

    [Fact]
    public void AModelThatIsNullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Check.Find(null!));
    }

    private static Model Document(out Node rule)
    {
        var model = PayrollSample.NewEmptyModel();
        var ruleset = model.Roots[0];
        ruleset.Set("name", "Payroll");

        rule = model.NewChild(ruleset, RulesLanguage.RulesSlot, RulesConceptKeys.Rule);
        rule.Set("name", "Approved");

        return model;
    }
}
